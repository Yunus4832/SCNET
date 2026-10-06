using System.Globalization;

using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public class SubsystemProjectiles : Subsystem, IUpdateable, IDrawable
{
    public const float BodyInflateAmount = 0.2f;

    private static readonly int[] _drawOrders = [10];

    private readonly DrawBlockEnvironmentData _drawBlockEnvironmentData = new();

    private readonly PrimitivesRenderer3D _primitivesRenderer = new();

    private readonly List<Projectile> _projectiles = [];

    private readonly List<Projectile> _projectilesToRemove = [];

    private int _nextNetworkId = 1;

    private uint _networkStateTick;

    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemBlockBehaviors _subsystemBlockBehaviors = null!;

    private SubsystemBodies _subsystemBodies = null!;

    private SubsystemExplosions _subsystemExplosions = null!;

    private SubsystemFireBlockBehavior _subsystemFireBlockBehavior = null!;

    private SubsystemFluidBlockBehavior _subsystemFluidBlockBehavior = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemNoise _subsystemNoise = null!;

    private SubsystemNetworkInterest _subsystemNetworkInterest = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemPickables _subsystemPickables = null!;

    private SubsystemSky _subsystemSky = null!;

    private SubsystemSoundMaterials _subsystemSoundMaterials = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    public ReadOnlyList<Projectile> Projectiles => new(_projectiles);

    public int[] DrawOrders => _drawOrders;

    public void Draw(Camera camera, int drawOrder)
    {
        _drawBlockEnvironmentData.SubsystemTerrain = _subsystemTerrain;
        _drawBlockEnvironmentData.InWorldMatrix = Matrix.Identity;
        var num = MathUtils.Sqr(_subsystemSky.VisibilityRange);
        foreach (var projectile in _projectiles)
        {
            var position = projectile.Position;
            if (projectile.NoChunk || !(Vector3.DistanceSquared(camera.ViewPosition, position) < num) ||
                !camera.ViewFrustum.Intersection(position))
            {
                continue;
            }

            var x = Terrain.ToCell(position.X);
            var num2 = Terrain.ToCell(position.Y);
            var z = Terrain.ToCell(position.Z);
            var num3 = Terrain.ExtractContents(projectile.Value);
            var block = BlocksManager.Blocks[num3];
            var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(x, z, false);
            if (chunkAtCell is { MainThreadState: >= TerrainChunkState.InvalidVertices1 } && num2 is >= 0 and < 255)
            {
                _drawBlockEnvironmentData.Humidity = _subsystemTerrain.Terrain.GetSeasonalHumidity(x, z);
                _drawBlockEnvironmentData.Temperature = _subsystemTerrain.Terrain.GetSeasonalTemperature(x, z) +
                                                        SubsystemWeather.GetTemperatureAdjustmentAtHeight(num2);
                projectile.Light = _subsystemTerrain.Terrain.GetCellLightFast(x, num2, z);
            }

            _drawBlockEnvironmentData.Light = projectile.Light;
            _drawBlockEnvironmentData.BillboardDirection =
                block.AlignToVelocity ? null : new Vector3?(camera.ViewDirection);
            _drawBlockEnvironmentData.InWorldMatrix.Translation = position;
            Matrix matrix;
            if (block.AlignToVelocity)
            {
                CalculateVelocityAlignMatrix(block, position, projectile.Velocity, out matrix);
            }
            else if (projectile.Rotation != Vector3.Zero)
            {
                matrix = Matrix.CreateFromAxisAngle(Vector3.Normalize(projectile.Rotation),
                    projectile.Rotation.Length());
                matrix.Translation = projectile.Position;
            }
            else
            {
                matrix = Matrix.CreateTranslation(projectile.Position);
            }

            block.DrawBlock(_primitivesRenderer, projectile.Value, Color.White, 0.3f, ref matrix,
                _drawBlockEnvironmentData);
        }

        _primitivesRenderer.Flush(camera.ViewProjectionMatrix);
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            UpdateReplicatedMotion(dt);
            return;
        }

        var totalElapsedGameTime = _subsystemGameInfo.TotalElapsedGameTime;
        foreach (var projectile in _projectiles)
        {
            if (projectile.ToRemove)
            {
                _projectilesToRemove.Add(projectile);
            }
            else
            {
                var block = BlocksManager.Blocks[Terrain.ExtractContents(projectile.Value)];
                if (totalElapsedGameTime - projectile.CreationTime > 40.0)
                {
                    projectile.ToRemove = true;
                }

                var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(
                    Terrain.ToCell(projectile.Position.X),
                    Terrain.ToCell(projectile.Position.Z),
                    false
                );
                if (chunkAtCell is not { MainThreadState: > TerrainChunkState.InvalidContents4 })
                {
                    projectile.NoChunk = true;
                    projectile.TrailParticleSystem?.IsStopped = true;
                }
                else
                {
                    projectile.NoChunk = false;
                    var position = projectile.Position;
                    var vector = position + projectile.Velocity * dt;
                    var v = block.ProjectileTipOffset * Vector3.Normalize(projectile.Velocity);
                    var bodyRaycastResult =
                        _subsystemBodies.Raycast(position + v, vector + v, 0.2f, (_, _) => true);
                    var terrainRaycastResult = _subsystemTerrain.Raycast(position + v, vector + v, false, true,
                        (value, _) => BlocksManager.Blocks[Terrain.ExtractContents(value)].Collidable);
                    var flag = block.DisintegratesOnHit;
                    if (terrainRaycastResult.HasValue || bodyRaycastResult.HasValue)
                    {
                        var cellFace = terrainRaycastResult.HasValue
                            ? new CellFace?(terrainRaycastResult.Value.CellFace)
                            : null;
                        var componentBody = bodyRaycastResult?.ComponentBody;
                        var blockBehaviors =
                            _subsystemBlockBehaviors.GetBlockBehaviors(Terrain.ExtractContents(projectile.Value));
                        foreach (var behavior in blockBehaviors)
                        {
                            flag |= behavior.OnHitAsProjectile(cellFace, componentBody!, projectile);
                        }

                        projectile.ToRemove |= flag;
                    }

                    Vector3? vector2 = null;
                    if (bodyRaycastResult.HasValue && (!terrainRaycastResult.HasValue ||
                                                       bodyRaycastResult.Value.Distance <
                                                       terrainRaycastResult.Value.Distance))
                    {
                        if (projectile.Velocity.Length() > 10f)
                        {
                            ComponentMiner.AttackBody(bodyRaycastResult.Value.ComponentBody, projectile.Owner,
                                bodyRaycastResult.Value.HitPoint(), Vector3.Normalize(projectile.Velocity),
                                block.GetProjectilePower(projectile.Value), false);
                            if (projectile.Owner is { PlayerStats: not null })
                            {
                                projectile.Owner.PlayerStats.RangedHits++;
                            }
                        }

                        if (projectile.IsIncendiary)
                        {
                            bodyRaycastResult.Value.ComponentBody.Entity.FindComponent<ComponentOnFire>()
                                ?.SetOnFire(projectile.Owner, _random.Float(6f, 8f));
                        }

                        vector = position;
                        projectile.Velocity *= -0.05f;
                        projectile.Velocity += _random.Vector3(0.33f * projectile.Velocity.Length());
                        projectile.AngularVelocity *= -0.05f;
                    }
                    else if (terrainRaycastResult.HasValue)
                    {
                        var cellFace2 = terrainRaycastResult.Value.CellFace;
                        var cellValue = _subsystemTerrain.Terrain.GetCellValue(cellFace2.X, cellFace2.Y, cellFace2.Z);
                        var num = Terrain.ExtractContents(cellValue);
                        var block2 = BlocksManager.Blocks[num];
                        var num2 = projectile.Velocity.Length();
                        var blockBehaviors2 = _subsystemBlockBehaviors.GetBlockBehaviors(num);
                        foreach (var behavior in blockBehaviors2)
                        {
                            behavior.OnHitByProjectile(cellFace2, projectile);
                        }

                        if (num2 > 10f && _random.Float(0f, 1f) > block2.ProjectileResilience)
                        {
                            _subsystemTerrain.DestroyCell(0, cellFace2.X, cellFace2.Y, cellFace2.Z, 0, true, false);
                            _subsystemSoundMaterials.PlayImpactSound(cellValue, position, 1f);
                            SendEffect(ProjectilePackage.EffectType.Impact, cellValue, position);
                        }

                        if (projectile.IsIncendiary)
                        {
                            _subsystemFireBlockBehavior.SetCellOnFire(terrainRaycastResult.Value.CellFace.X,
                                terrainRaycastResult.Value.CellFace.Y, terrainRaycastResult.Value.CellFace.Z, 1f);
                            var vector3 = projectile.Position - 0.75f * Vector3.Normalize(projectile.Velocity);
                            for (var k = 0; k < 8; k++)
                            {
                                var v2 = k == 0 ? Vector3.Normalize(projectile.Velocity) : _random.Vector3(1.5f);
                                var terrainRaycastResult2 = _subsystemTerrain.Raycast(vector3, vector3 + v2, false,
                                    true, (_, _) => true);
                                if (terrainRaycastResult2.HasValue)
                                {
                                    _subsystemFireBlockBehavior.SetCellOnFire(terrainRaycastResult2.Value.CellFace.X,
                                        terrainRaycastResult2.Value.CellFace.Y, terrainRaycastResult2.Value.CellFace.Z,
                                        1f);
                                }
                            }
                        }

                        if (num2 > 5f)
                        {
                            _subsystemSoundMaterials.PlayImpactSound(cellValue, position, 1f);
                            SendEffect(ProjectilePackage.EffectType.Impact, cellValue, position);
                        }

                        if (block.Stickable && num2 > 10f && _random.Bool(block2.ProjectileStickProbability))
                        {
                            var v3 = Vector3.Normalize(projectile.Velocity);
                            var s = MathUtils.Lerp(0.1f, 0.2f, MathUtils.Saturate((num2 - 15f) / 20f));
                            vector2 = position +
                                      terrainRaycastResult.Value.Distance * Vector3.Normalize(projectile.Velocity) +
                                      v3 * s;
                        }
                        else
                        {
                            var plane = cellFace2.CalculatePlane();
                            vector = position;
                            if (plane.Normal.X != 0f)
                            {
                                projectile.Velocity *= new Vector3(-0.3f, 0.3f, 0.3f);
                            }

                            if (plane.Normal.Y != 0f)
                            {
                                projectile.Velocity *= new Vector3(0.3f, -0.3f, 0.3f);
                            }

                            if (plane.Normal.Z != 0f)
                            {
                                projectile.Velocity *= new Vector3(0.3f, 0.3f, -0.3f);
                            }

                            var num3 = projectile.Velocity.Length();
                            projectile.Velocity =
                                num3 * Vector3.Normalize(projectile.Velocity + _random.Vector3(num3 / 6f, num3 / 3f));
                            projectile.AngularVelocity *= -0.3f;
                        }

                        MakeProjectileNoise(projectile);
                    }

                    if (terrainRaycastResult.HasValue || bodyRaycastResult.HasValue)
                    {
                        if (flag)
                        {
                            _subsystemParticles.AddParticleSystem(block.CreateDebrisParticleSystem(_subsystemTerrain,
                                projectile.Position, projectile.Value, 1f));
                            SendEffect(ProjectilePackage.EffectType.Debris, projectile.Value, projectile.Position);
                        }
                        else if (!projectile.ToRemove && (vector2.HasValue || projectile.Velocity.Length() < 1f))
                        {
                            if (projectile.ProjectileStoppedAction == ProjectileStoppedAction.TurnIntoPickable)
                            {
                                var num4 = BlocksManager.DamageItem(projectile.Value, 1);
                                if (num4 != 0)
                                {
                                    if (vector2.HasValue)
                                    {
                                        CalculateVelocityAlignMatrix(block, vector2.Value, projectile.Velocity,
                                            out var matrix);
                                        _subsystemPickables.AddPickable(num4, 1, projectile.Position, Vector3.Zero,
                                            matrix);
                                    }
                                    else
                                    {
                                        _subsystemPickables.AddPickable(num4, 1, position, Vector3.Zero, null);
                                    }
                                }

                                projectile.ToRemove = true;
                            }
                            else if (projectile.ProjectileStoppedAction == ProjectileStoppedAction.Disappear)
                            {
                                projectile.ToRemove = true;
                            }
                        }
                    }

                    var num5 = projectile.IsInWater
                        ? MathUtils.Pow(0.001f, dt)
                        : MathUtils.Pow(block.ProjectileDamping, dt);
                    projectile.Velocity.Y += -10f * dt;
                    projectile.Velocity *= num5;
                    projectile.AngularVelocity *= num5;
                    projectile.Position = vector;
                    projectile.Rotation += projectile.AngularVelocity * dt;
                    if (projectile.TrailParticleSystem != null)
                    {
                        if (!_subsystemParticles.ContainsParticleSystem(
                                (ParticleSystemBase)projectile.TrailParticleSystem))
                        {
                            _subsystemParticles.AddParticleSystem((ParticleSystemBase)projectile.TrailParticleSystem);
                        }

                        var v4 = projectile.TrailOffset != Vector3.Zero
                            ? Vector3.TransformNormal(projectile.TrailOffset,
                                Matrix.CreateFromAxisAngle(Vector3.Normalize(projectile.Rotation),
                                    projectile.Rotation.Length()))
                            : Vector3.Zero;
                        projectile.TrailParticleSystem.Position = projectile.Position + v4;
                        if (projectile.IsInWater)
                        {
                            projectile.TrailParticleSystem.IsStopped = true;
                        }
                    }

                    var flag2 = IsWater(projectile.Position);
                    if (projectile.IsInWater != flag2)
                    {
                        if (flag2)
                        {
                            var num6 = new Vector2(projectile.Velocity.X + projectile.Velocity.Z).Length();
                            if (num6 > 6f && num6 > 4f * MathUtils.Abs(projectile.Velocity.Y))
                            {
                                projectile.Velocity *= 0.5f;
                                projectile.Velocity.Y *= -1f;
                                flag2 = false;
                            }
                            else
                            {
                                projectile.Velocity *= 0.2f;
                            }

                            var surfaceHeight = _subsystemFluidBlockBehavior.GetSurfaceHeight(
                                Terrain.ToCell(projectile.Position.X), Terrain.ToCell(projectile.Position.Y),
                                Terrain.ToCell(projectile.Position.Z));
                            if (surfaceHeight.HasValue)
                            {
                                _subsystemParticles.AddParticleSystem(new WaterSplashParticleSystem(_subsystemTerrain,
                                    new Vector3(projectile.Position.X, surfaceHeight.Value, projectile.Position.Z),
                                    false));
                                _subsystemAudio.PlayRandomSound("Audio/Splashes", 1f, _random.Float(-0.2f, 0.2f),
                                    projectile.Position, 6f, true);
                                SendEffect(ProjectilePackage.EffectType.WaterSplash, projectile.Value,
                                    new Vector3(projectile.Position.X, surfaceHeight.Value, projectile.Position.Z));
                                MakeProjectileNoise(projectile);
                            }
                        }

                        projectile.IsInWater = flag2;
                    }

                    if (IsMagma(projectile.Position))
                    {
                        _subsystemParticles.AddParticleSystem(
                            new MagmaSplashParticleSystem(_subsystemTerrain, projectile.Position, false));
                        _subsystemAudio.PlayRandomSound("Audio/Sizzles", 1f, _random.Float(-0.2f, 0.2f),
                            projectile.Position, 3f, true);
                        SendEffect(ProjectilePackage.EffectType.MagmaSplash, projectile.Value, projectile.Position);
                        projectile.ToRemove = true;
                        _subsystemExplosions.TryExplodeBlock(Terrain.ToCell(projectile.Position.X),
                            Terrain.ToCell(projectile.Position.Y), Terrain.ToCell(projectile.Position.Z),
                            projectile.Value);
                    }

                    if (_subsystemTime.PeriodicGameTimeEvent(1.0, projectile.GetHashCode() % 100 / 100.0) &&
                        (_subsystemFireBlockBehavior.IsCellOnFire(Terrain.ToCell(projectile.Position.X),
                             Terrain.ToCell(projectile.Position.Y + 0.1f), Terrain.ToCell(projectile.Position.Z)) ||
                         _subsystemFireBlockBehavior.IsCellOnFire(Terrain.ToCell(projectile.Position.X),
                             Terrain.ToCell(projectile.Position.Y + 0.1f) - 1, Terrain.ToCell(projectile.Position.Z))))
                    {
                        _subsystemAudio.PlayRandomSound("Audio/Sizzles", 1f, _random.Float(-0.2f, 0.2f),
                            projectile.Position, 3f, true);
                        SendEffect(ProjectilePackage.EffectType.Sizzle, projectile.Value, projectile.Position);
                        projectile.ToRemove = true;
                        _subsystemExplosions.TryExplodeBlock(Terrain.ToCell(projectile.Position.X),
                            Terrain.ToCell(projectile.Position.Y), Terrain.ToCell(projectile.Position.Z),
                            projectile.Value);
                    }
                }
            }
        }

        foreach (var item in _projectilesToRemove)
        {
            if (item.TrailParticleSystem != null)
            {
                item.TrailParticleSystem.IsStopped = true;
            }

            _projectiles.Remove(item);
            if (CommonLib.WorkType == WorkType.Server)
            {
                SendProjectileRemoval(item);
            }

            ProjectileRemoved?.Invoke(item);
        }

        _projectilesToRemove.Clear();
        if (CommonLib.WorkType == WorkType.Server && Time.PeriodicEvent(0.1, 0.0))
        {
            UpdateClientProjectileInterests();
        }
    }

    private void SendEffect(ProjectilePackage.EffectType effect, int value, Vector3 position)
    {
        if (CommonLib.WorkType != WorkType.Server)
        {
            return;
        }

        var package = new ProjectilePackage
        {
            Type = ProjectilePackage.EventType.Effect,
            Effect = effect,
            Value = value,
            Position = position
        };
        if (effect == ProjectilePackage.EffectType.Debris)
        {
            var observers = _subsystemNetworkInterest.GetObservers(position.XZ).ToArray();
            if (observers.Length > 0)
            {
                CommonLib.Net.QueuePackage(package, PackageAudience.To(observers));
            }

            return;
        }

        var minDistance = effect == ProjectilePackage.EffectType.Impact ? 5f :
            effect == ProjectilePackage.EffectType.WaterSplash ? 6f : 3f;
        var volume = effect == ProjectilePackage.EffectType.Impact ? 0.5f : 1f;
        var audibleRadius = SubsystemAudio.CalculateAudibleRadius(volume, minDistance);
        NetworkSender.SendNearPoint(Project, position, audibleRadius, package);
    }

    public void PlayReplicatedEffect(ProjectilePackage.EffectType effect, int value, Vector3 position)
    {
        switch (effect)
        {
            case ProjectilePackage.EffectType.Impact:
                _subsystemSoundMaterials.PlayImpactSound(value, position, 1f);
                break;
            case ProjectilePackage.EffectType.Debris:
                _subsystemParticles.AddParticleSystem(BlocksManager.Blocks[Terrain.ExtractContents(value)]
                    .CreateDebrisParticleSystem(_subsystemTerrain, position, value, 1f));
                break;
            case ProjectilePackage.EffectType.WaterSplash:
                _subsystemParticles.AddParticleSystem(new WaterSplashParticleSystem(_subsystemTerrain, position, false));
                _subsystemAudio.PlayRandomSound("Audio/Splashes", 1f, _random.Float(-0.2f, 0.2f), position, 6f, true);
                break;
            case ProjectilePackage.EffectType.MagmaSplash:
                _subsystemParticles.AddParticleSystem(new MagmaSplashParticleSystem(_subsystemTerrain, position, false));
                goto case ProjectilePackage.EffectType.Sizzle;
            case ProjectilePackage.EffectType.Sizzle:
                _subsystemAudio.PlayRandomSound("Audio/Sizzles", 1f, _random.Float(-0.2f, 0.2f), position, 3f, true);
                break;
        }
    }

    private void UpdateReplicatedMotion(float dt)
    {
        foreach (var projectile in _projectiles)
        {
            var chunk = _subsystemTerrain.Terrain.GetChunkAtCell(
                Terrain.ToCell(projectile.Position.X), Terrain.ToCell(projectile.Position.Z), false);
            projectile.NoChunk = chunk is not { MainThreadState: > TerrainChunkState.InvalidContents4 };
            if (projectile.NoChunk)
            {
                projectile.TrailParticleSystem?.IsStopped = true;
                continue;
            }

            var block = BlocksManager.Blocks[Terrain.ExtractContents(projectile.Value)];
            projectile.Position += projectile.Velocity * dt;
            projectile.IsInWater = IsWater(projectile.Position);
            var damping = MathUtils.Pow(projectile.IsInWater ? 0.001f : block.ProjectileDamping, dt);
            projectile.Velocity.Y -= 10f * dt;
            projectile.Velocity *= damping;
            projectile.AngularVelocity *= damping;
            projectile.Rotation += projectile.AngularVelocity * dt;
            if (projectile.TrailParticleSystem is not { } trail)
            {
                continue;
            }

            if (!_subsystemParticles.ContainsParticleSystem((ParticleSystemBase)trail))
            {
                _subsystemParticles.AddParticleSystem((ParticleSystemBase)trail);
            }

            var rotation = projectile.Rotation.LengthSquared() > 0f
                ? Matrix.CreateFromAxisAngle(Vector3.Normalize(projectile.Rotation), projectile.Rotation.Length())
                : Matrix.Identity;
            trail.Position = projectile.Position + Vector3.TransformNormal(projectile.TrailOffset, rotation);
            trail.IsStopped = projectile.IsInWater;
        }
    }

    public event Action<Projectile>? ProjectileAdded;
    public event Action<Projectile>? ProjectileRemoved;

    public Projectile? AddProjectile(
        int value,
        Vector3 position,
        Vector3 velocity,
        Vector3 angularVelocity,
        ComponentCreature? owner
    )
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return null;
        }

        var proj = AddProjectileNet(value, position, velocity, angularVelocity, owner);
        return proj;
    }

    public Projectile AddProjectileNet(
        int value,
        Vector3 position,
        Vector3 velocity,
        Vector3 angularVelocity,
        ComponentCreature? owner,
        int networkId = 0
    )
    {
        var projectile = new Projectile
        {
            NetworkId = networkId != 0 ? networkId : AllocateNetworkId(),
            Value = value,
            Position = position,
            Velocity = velocity,
            Rotation = Vector3.Zero,
            AngularVelocity = angularVelocity,
            CreationTime = _subsystemGameInfo.TotalElapsedGameTime,
            IsInWater = IsWater(position),
            Owner = owner,
            ProjectileStoppedAction = ProjectileStoppedAction.TurnIntoPickable
        };
        _projectiles.Add(projectile);
        ProjectileAdded?.Invoke(projectile);
        if (CommonLib.WorkType != WorkType.Client && owner is { PlayerStats: not null })
        {
            owner.PlayerStats.RangedAttacks++;
        }

        return projectile;
    }

    public Projectile? FireProjectile(int value, Vector3 position, Vector3 velocity, Vector3 angularVelocity,
        ComponentCreature? owner)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return null;
        }

        var proj = FireProjectileNet(value, position, velocity, angularVelocity, owner);
        if (proj == null)
        {
            return proj;
        }

        proj.IsFireProjectile = true;

        return proj;
    }

    public Projectile? FireProjectileNet(int value, Vector3 position, Vector3 velocity, Vector3 angularVelocity,
        ComponentCreature? owner,
        int networkId = 0)
    {
        var num = Terrain.ExtractContents(value);
        var block = BlocksManager.Blocks[num];
        var v = Vector3.Normalize(velocity);
        var vector = position;
        if (owner != null)
        {
            var ray = new Ray3(position + v * 5f, -v);
            var boundingBox = owner.ComponentBody.BoundingBox;
            boundingBox.Min -= new Vector3(0.4f);
            boundingBox.Max += new Vector3(0.4f);
            var num2 = ray.Intersection(boundingBox);
            if (num2.HasValue)
            {
                if (num2.Value == 0f)
                {
                    return null;
                }

                vector = position + v * (5f - num2.Value + 0.1f);
            }
        }

        var end = vector + v * block.ProjectileTipOffset;
        if (_subsystemTerrain.Raycast(position, end, false, true,
                (testValue, _) => BlocksManager.Blocks[Terrain.ExtractContents(testValue)].Collidable)
            .HasValue)
        {
            return null;
        }

        var projectile = AddProjectileNet(value, vector, velocity, angularVelocity, owner, networkId);
        InitializeFiredProjectile(projectile);
        return projectile;
    }

    public Projectile AddReplicatedProjectile(ProjectilePackage package, ComponentCreature? owner)
    {
        var projectile = AddProjectileNet(
            package.Value,
            package.Position,
            package.Velocity,
            package.AngularVelocity,
            owner,
            package.NetworkId);
        if (package.IsFireProjectile)
        {
            InitializeFiredProjectile(projectile);
        }

        projectile.TrailOffset = package.TrailOffset;
        projectile.Rotation = package.Rotation;
        projectile.LastNetworkStateTick = package.StateTick;
        return projectile;
    }

    internal static bool ApplyNetworkMotion(Projectile projectile, ProjectilePackage package)
    {
        if (unchecked((int)(package.StateTick - projectile.LastNetworkStateTick)) <= 0)
        {
            return false;
        }

        projectile.LastNetworkStateTick = package.StateTick;
        projectile.Position = package.Position;
        projectile.Rotation = package.Rotation;
        projectile.Velocity = package.Velocity;
        projectile.AngularVelocity = package.AngularVelocity;
        return true;
    }

    private void InitializeFiredProjectile(Projectile projectile)
    {
        projectile.IsFireProjectile = true;
        var num = Terrain.ExtractContents(projectile.Value);
        var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(num);
        foreach (var behavior in blockBehaviors)
        {
            behavior.OnFiredAsProjectile(projectile);
        }

    }

    public void AddTrail(Projectile projectile, Vector3 offset, ITrailParticleSystem particleSystem)
    {
        RemoveTrail(projectile);
        projectile.TrailParticleSystem = particleSystem;
        projectile.TrailOffset = offset;
    }

    private void RemoveTrail(Projectile projectile)
    {
        if (projectile.TrailParticleSystem == null)
        {
            return;
        }

        if (_subsystemParticles.ContainsParticleSystem((ParticleSystemBase)projectile.TrailParticleSystem))
        {
            _subsystemParticles.RemoveParticleSystem((ParticleSystemBase)projectile.TrailParticleSystem);
        }

        projectile.TrailParticleSystem = null;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true)!;
        _subsystemNetworkInterest = Project.FindSubsystem<SubsystemNetworkInterest>(true)!;
        _subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true)!;
        _subsystemFluidBlockBehavior = Project.FindSubsystem<SubsystemFluidBlockBehavior>(true)!;
        _subsystemFireBlockBehavior = Project.FindSubsystem<SubsystemFireBlockBehavior>(true)!;
        foreach (ValuesDictionary item in valuesDictionary.GetValue<ValuesDictionary>("Projectiles").Values
                     .Where(v => v is ValuesDictionary))
        {
            var projectile = new Projectile
            {
                NetworkId = item.GetValue("NetworkId", 0),
                Value = item.GetValue<int>("Value"),
                Position = item.GetValue<Vector3>("Position"),
                Velocity = item.GetValue<Vector3>("Velocity"),
                CreationTime = item.GetValue<double>("CreationTime")
            };
            if (projectile.NetworkId == 0)
            {
                projectile.NetworkId = AllocateNetworkId();
            }
            else
            {
                _nextNetworkId = Math.Max(_nextNetworkId, projectile.NetworkId + 1);
            }

            _projectiles.Add(projectile);
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("Projectiles", valuesDictionary2);
        if (Project.SendToClientMode)
        {
            return;
        }

        var num = 0;
        foreach (var projectile in _projectiles)
        {
            var valuesDictionary3 = new ValuesDictionary();
            valuesDictionary2.SetValue(num.ToString(CultureInfo.InvariantCulture), valuesDictionary3);
            valuesDictionary3.SetValue("Value", projectile.Value);
            valuesDictionary3.SetValue("NetworkId", projectile.NetworkId);
            valuesDictionary3.SetValue("Position", projectile.Position);
            valuesDictionary3.SetValue("Velocity", projectile.Velocity);
            valuesDictionary3.SetValue("CreationTime", projectile.CreationTime);
            num++;
        }
    }

    public Projectile? FindProjectile(int networkId)
    {
        return _projectiles.Find(projectile => projectile.NetworkId == networkId);
    }

    public void RemoveProjectileNet(int networkId)
    {
        var projectile = FindProjectile(networkId);
        if (projectile is null)
        {
            return;
        }

        RemoveTrail(projectile);
        _projectiles.Remove(projectile);
        ProjectileRemoved?.Invoke(projectile);
    }

    private int AllocateNetworkId()
    {
        return _nextNetworkId++;
    }

    private void SendProjectileRemoval(Projectile projectile)
    {
        var observers = _subsystemNetworkInterest.Entities
            .GetObservers(EntityInterestGroup.Projectiles, projectile.NetworkId)
            .Where(client => client.IsConnected)
            .ToArray();
        if (observers.Length > 0)
        {
            CommonLib.Net.QueuePackage(
                new ProjectilePackage(projectile.NetworkId),
                PackageAudience.To(observers));
        }

        _subsystemNetworkInterest.Entities.RemoveEntity(
            EntityInterestGroup.Projectiles,
            projectile.NetworkId);
    }

    private void UpdateClientProjectileInterests()
    {
        _networkStateTick++;
        var projectiles = _projectiles.ToDictionary(projectile => projectile.NetworkId);
        var relevant = _subsystemNetworkInterest.GetPointCandidates(projectiles.Values.Select(projectile =>
            (projectile.NetworkId, projectile.Position.XZ)));
        foreach (var client in CommonLib.Net.Clients.Values)
        {
            if (client == CommonLib.Net.Self || !client.IsConnected)
            {
                continue;
            }

            var current = relevant.TryGetValue(client, out var ids) ? ids : [];
            var changes = _subsystemNetworkInterest.Entities.Synchronize(
                client,
                EntityInterestGroup.Projectiles,
                current,
                projectiles.ContainsKey,
                3);
            foreach (var networkId in changes.Entered)
            {
                if (projectiles.TryGetValue(networkId, out var projectile))
                {
                    CommonLib.Net.QueuePackage(
                        new ProjectilePackage(projectile) { StateTick = _networkStateTick },
                        PackageAudience.To(client));
                }
            }

            foreach (var networkId in changes.Left)
            {
                CommonLib.Net.QueuePackage(
                    new ProjectilePackage(networkId),
                    PackageAudience.To(client));
            }

            foreach (var networkId in current.Except(changes.Entered))
            {
                var projectile = projectiles[networkId];
                CommonLib.Net.QueuePackage(
                    new ProjectilePackage(projectile)
                    {
                        Type = ProjectilePackage.EventType.Update,
                        StateTick = _networkStateTick
                    },
                    PackageAudience.To(client));
            }
        }
    }

    private bool IsWater(Vector3 position)
    {
        var cellContents = _subsystemTerrain.Terrain.GetCellContents(Terrain.ToCell(position.X),
            Terrain.ToCell(position.Y), Terrain.ToCell(position.Z));
        return BlocksManager.Blocks[cellContents] is WaterBlock;
    }

    private bool IsMagma(Vector3 position)
    {
        var cellContents = _subsystemTerrain.Terrain.GetCellContents(Terrain.ToCell(position.X),
            Terrain.ToCell(position.Y), Terrain.ToCell(position.Z));
        return BlocksManager.Blocks[cellContents] is MagmaBlock;
    }

    private void MakeProjectileNoise(Projectile projectile)
    {
        if (!(_subsystemTime.GameTime - projectile.LastNoiseTime > 0.5))
        {
            return;
        }

        _subsystemNoise.MakeNoise(projectile.Position, 0.25f, 6f);
        projectile.LastNoiseTime = _subsystemTime.GameTime;
    }

    private static void CalculateVelocityAlignMatrix(
        Block projectileBlock,
        Vector3 position,
        Vector3 velocity,
        out Matrix matrix
    )
    {
        matrix = Matrix.Identity;
        matrix.Up = Vector3.Normalize(velocity);
        matrix.Right = Vector3.Normalize(Vector3.Cross(matrix.Up, Vector3.UnitY));
        matrix.Forward = Vector3.Normalize(Vector3.Cross(matrix.Up, matrix.Right));
        matrix.Translation = position;
    }
}
