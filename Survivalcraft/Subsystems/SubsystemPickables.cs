using Engine.Graphics;
using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Subsystems;

public class SubsystemPickables : Subsystem, IDrawable, IUpdateable
{
    private static readonly int[] _drawOrders = [10];

    private readonly DrawBlockEnvironmentData _drawBlockEnvironmentData = new();

    private readonly List<Pickable> _pickables = [];

    public readonly List<Pickable> PickablesToRemove = [];

    private readonly PrimitivesRenderer3D _primitivesRenderer = new();

    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemBlockBehaviors _subsystemBlockBehaviors = null!;

    private SubsystemExplosions _subsystemExplosions = null!;

    private SubsystemFireBlockBehavior _subsystemFireBlockBehavior = null!;

    private SubsystemFluidBlockBehavior _subsystemFluidBlockBehavior = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemPlayers _subsystemPlayers = null!;

    private SubsystemSky _subsystemSky = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private readonly List<ComponentPlayer> _tmpPlayers = [];

    public List<ushort> UseMaps = [];

    public ReadOnlyList<Pickable> Pickables => new(_pickables);

    public int[] DrawOrders => _drawOrders;

    public void Draw(Camera camera, int drawOrder)
    {
        var totalElapsedGameTime = _subsystemGameInfo.TotalElapsedGameTime;
        _drawBlockEnvironmentData.SubsystemTerrain = _subsystemTerrain;
        var matrix = Matrix.CreateRotationY((float)MathUtils.Remainder(totalElapsedGameTime, 6.2831854820251465));
        var num = MathUtils.Min(_subsystemSky.VisibilityRange, 30f);
        foreach (var pickable in _pickables)
        {
            var position = pickable.Position;
            var num2 = Vector3.Dot(camera.ViewDirection, position - camera.ViewPosition);
            if (!(num2 > -0.5f) || !(num2 < num))
            {
                continue;
            }

            var num3 = Terrain.ExtractContents(pickable.Value);
            var block = BlocksManager.Blocks[num3];
            var num4 = (float)(totalElapsedGameTime - pickable.CreationTime);
            if (!pickable.StuckMatrix.HasValue)
            {
                position.Y += 0.25f * MathUtils.Saturate(3f * num4);
            }

            var x = Terrain.ToCell(position.X);
            var num5 = Terrain.ToCell(position.Y);
            var z = Terrain.ToCell(position.Z);
            var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(x, z, false);
            if (chunkAtCell is { State: >= TerrainChunkState.InvalidVertices1 } && num5 is >= 0 and < 511)
            {
                _drawBlockEnvironmentData.Humidity = _subsystemTerrain.Terrain.GetSeasonalHumidity(x, z);
                _drawBlockEnvironmentData.Temperature = _subsystemTerrain.Terrain.GetSeasonalTemperature(x, z) +
                                                        SubsystemWeather.GetTemperatureAdjustmentAtHeight(num5);
                var f = MathUtils.Max(position.Y - num5 - 0.75f, 0f) / 0.25f;
                pickable.Light = (int)MathUtils.Lerp(
                    _subsystemTerrain.Terrain.GetCellLightFast(x, num5, z),
                    _subsystemTerrain.Terrain.GetCellLightFast(x, num5 + 1, z), f);
            }

            _drawBlockEnvironmentData.Light = pickable.Light;
            _drawBlockEnvironmentData.BillboardDirection = pickable.Position - camera.ViewPosition;
            _drawBlockEnvironmentData.InWorldMatrix.Translation = position;
            if (pickable.StuckMatrix.HasValue)
            {
                var matrix2 = pickable.StuckMatrix.Value;
                block.DrawBlock(_primitivesRenderer, pickable.Value, Color.White, 0.3f, ref matrix2,
                    _drawBlockEnvironmentData);
            }
            else
            {
                matrix.Translation = position + new Vector3(0f, 0.04f * MathUtils.Sin(3f * num4), 0f);
                block.DrawBlock(_primitivesRenderer, pickable.Value, Color.White, 0.3f, ref matrix,
                    _drawBlockEnvironmentData);
            }
        }

        _primitivesRenderer.Flush(camera.ViewProjectionMatrix);
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        var totalElapsedGameTime = _subsystemGameInfo.TotalElapsedGameTime;
        var num = MathUtils.Pow(0.5f, dt);
        var num2 = MathUtils.Pow(0.001f, dt);
        _tmpPlayers.Clear();
        foreach (var componentPlayer in _subsystemPlayers.ComponentPlayers)
        {
            if (componentPlayer.ComponentHealth.Health > 0f)
            {
                _tmpPlayers.Add(componentPlayer);
            }
        }

        foreach (var pickable in _pickables)
        {
            if (pickable.ToRemove)
            {
                PickablesToRemove.Add(pickable);
                continue;
            }

            var block = BlocksManager.Blocks[Terrain.ExtractContents(pickable.Value)];
            var num3 = _pickables.Count - PickablesToRemove.Count;
            var num4 = MathUtils.Lerp(300f, 90f, MathUtils.Saturate(num3 / 60f));
            var num5 = totalElapsedGameTime - pickable.CreationTime;
            if (num5 > num4)
            {
                pickable.ToRemove = true;
            }
            else
            {
                var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(
                    Terrain.ToCell(pickable.Position.X),
                    Terrain.ToCell(pickable.Position.Z),
                    false
                );
                if (chunkAtCell is not { State: > TerrainChunkState.InvalidContents4 })
                {
                    continue;
                }

                var position = pickable.Position;
                var vector = position + pickable.Velocity * dt;
                if (!pickable.FlyToPosition.HasValue && num5 > 0.5)
                {
                    foreach (var tmpPlayer in _tmpPlayers)
                    {
                        var componentBody = tmpPlayer.ComponentBody;
                        var v = componentBody.Position + new Vector3(0f, 0.75f, 0f);
                        var num6 = (v - pickable.Position).LengthSquared();
                        if (!(num6 < 3.0625f))
                        {
                            continue;
                        }

                        //玩家获取到Pickable
                        pickable.GetPickPlayer = tmpPlayer.PlayerData.ClientId;
                        pickable.PositionFix = v;
                        pickable.Distance = num6;
                        if (CommonLib.WorkType != WorkType.Client)
                        {
                            OnPlayerGetPickable(pickable, tmpPlayer, v, num6);
                        }
                    }
                }

                if (pickable.FlyToPosition.HasValue)
                {
                    var v2 = pickable.FlyToPosition.Value - pickable.Position;
                    var num7 = v2.LengthSquared();
                    if (num7 >= 0.25f)
                    {
                        pickable.Velocity = 6f * v2 / MathUtils.Sqrt(num7);
                    }
                    else
                    {
                        pickable.FlyToPosition = null;
                    }
                }
                else
                {
                    var vector2 = _subsystemFluidBlockBehavior.CalculateFlowSpeed(
                        Terrain.ToCell(pickable.Position.X), Terrain.ToCell(pickable.Position.Y + 0.1f),
                        Terrain.ToCell(pickable.Position.Z), out var surfaceBlock, out var surfaceHeight);
                    if (!pickable.StuckMatrix.HasValue)
                    {
                        var terrainRaycastResult = _subsystemTerrain.Raycast(position, vector, false, true,
                            (value, _) => BlocksManager.Blocks[Terrain.ExtractContents(value)].Collidable);
                        if (terrainRaycastResult.HasValue)
                        {
                            var contents = Terrain.ExtractContents(_subsystemTerrain.Terrain.GetCellValue(
                                terrainRaycastResult.Value.CellFace.X, terrainRaycastResult.Value.CellFace.Y,
                                terrainRaycastResult.Value.CellFace.Z));
                            var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(contents);
                            foreach (var behavior in blockBehaviors)
                            {
                                behavior.OnHitByProjectile(terrainRaycastResult.Value.CellFace, pickable);
                            }

                            if (_subsystemTerrain.Raycast(position, position, false, true,
                                    (value2, _) =>
                                        BlocksManager.Blocks[Terrain.ExtractContents(value2)].Collidable)
                                .HasValue)
                            {
                                var num8 = Terrain.ToCell(position.X);
                                var num9 = Terrain.ToCell(position.Y);
                                var num10 = Terrain.ToCell(position.Z);
                                var num11 = 0;
                                var num12 = 0;
                                var num13 = 0;
                                int? num14 = null;
                                for (var j = -3; j <= 3; j++)
                                for (var k = -3; k <= 3; k++)
                                for (var l = -3; l <= 3; l++)
                                {
                                    if (!BlocksManager
                                            .Blocks[
                                                _subsystemTerrain.Terrain.GetCellContents(j + num8, k + num9,
                                                    l + num10)].Collidable)
                                    {
                                        var num15 = j * j + k * k + l * l;
                                        if (num15 >= num14)
                                        {
                                            continue;
                                        }

                                        num11 = j + num8;
                                        num12 = k + num9;
                                        num13 = l + num10;
                                        num14 = num15;
                                    }
                                }

                                if (num14.HasValue)
                                {
                                    pickable.FlyToPosition = new Vector3(num11, num12, num13) + new Vector3(0.5f);
                                }
                                else
                                {
                                    pickable.ToRemove = true;
                                }
                            }
                            else
                            {
                                var plane = terrainRaycastResult.Value.CellFace.CalculatePlane();
                                var flag2 = vector2.HasValue && vector2.Value != Vector2.Zero;
                                if (plane.Normal.X != 0f)
                                {
                                    var num16 = flag2 || MathUtils.Sqrt(MathUtils.Sqr(pickable.Velocity.Y) +
                                                                        MathUtils.Sqr(pickable.Velocity.Z)) > 10f
                                        ? 0.95f
                                        : 0.25f;
                                    pickable.Velocity *= new Vector3(0f - num16, num16, num16);
                                }

                                if (plane.Normal.Y != 0f)
                                {
                                    var num17 = flag2 || MathUtils.Sqrt(MathUtils.Sqr(pickable.Velocity.X) +
                                                                        MathUtils.Sqr(pickable.Velocity.Z)) > 10f
                                        ? 0.95f
                                        : 0.25f;
                                    pickable.Velocity *= new Vector3(num17, 0f - num17, num17);
                                    if (flag2)
                                    {
                                        pickable.Velocity.Y += 0.1f * plane.Normal.Y;
                                    }
                                }

                                if (plane.Normal.Z != 0f)
                                {
                                    var num18 = flag2 || MathUtils.Sqrt(MathUtils.Sqr(pickable.Velocity.X) +
                                                                        MathUtils.Sqr(pickable.Velocity.Y)) > 10f
                                        ? 0.95f
                                        : 0.25f;
                                    pickable.Velocity *= new Vector3(num18, num18, 0f - num18);
                                }

                                vector = position;
                            }
                        }
                    }
                    else
                    {
                        var vector3 = pickable.StuckMatrix.Value.Translation +
                                      pickable.StuckMatrix.Value.Up * block.ProjectileTipOffset;
                        if (!_subsystemTerrain.Raycast(vector3, vector3, false, true,
                                (value, _) =>
                                    BlocksManager.Blocks[Terrain.ExtractContents(value)].Collidable).HasValue)
                        {
                            pickable.Position = pickable.StuckMatrix.Value.Translation;
                            pickable.Velocity = Vector3.Zero;
                            pickable.StuckMatrix = null;
                        }
                    }

                    if (surfaceBlock is WaterBlock && !pickable.SplashGenerated)
                    {
                        _subsystemParticles.AddParticleSystem(
                            new WaterSplashParticleSystem(_subsystemTerrain, pickable.Position, false));
                        _subsystemAudio.PlayRandomSound("Audio/Splashes", 1f, _random.Float(-0.2f, 0.2f),
                            pickable.Position, 6f, true);
                        pickable.SplashGenerated = true;
                    }
                    else if (surfaceBlock is MagmaBlock && !pickable.SplashGenerated)
                    {
                        _subsystemParticles.AddParticleSystem(
                            new MagmaSplashParticleSystem(_subsystemTerrain, pickable.Position, false));
                        _subsystemAudio.PlayRandomSound("Audio/Sizzles", 1f, _random.Float(-0.2f, 0.2f),
                            pickable.Position, 3f, true);
                        pickable.ToRemove = true;
                        pickable.SplashGenerated = true;
                        _subsystemExplosions.TryExplodeBlock(Terrain.ToCell(pickable.Position.X),
                            Terrain.ToCell(pickable.Position.Y), Terrain.ToCell(pickable.Position.Z),
                            pickable.Value);
                    }
                    else if (surfaceBlock == null)
                    {
                        pickable.SplashGenerated = false;
                    }

                    if (_subsystemTime.PeriodicGameTimeEvent(1.0, pickable.GetHashCode() % 100 / 100.0) &&
                        (_subsystemTerrain.Terrain.GetCellContents(Terrain.ToCell(pickable.Position.X),
                                Terrain.ToCell(pickable.Position.Y + 0.1f), Terrain.ToCell(pickable.Position.Z)) ==
                            104 || _subsystemFireBlockBehavior.IsCellOnFire(Terrain.ToCell(pickable.Position.X),
                                Terrain.ToCell(pickable.Position.Y + 0.1f), Terrain.ToCell(pickable.Position.Z))))
                    {
                        _subsystemAudio.PlayRandomSound("Audio/Sizzles", 1f, _random.Float(-0.2f, 0.2f),
                            pickable.Position, 3f, true);
                        pickable.ToRemove = true;
                        _subsystemExplosions.TryExplodeBlock(Terrain.ToCell(pickable.Position.X),
                            Terrain.ToCell(pickable.Position.Y), Terrain.ToCell(pickable.Position.Z),
                            pickable.Value);
                    }

                    if (!pickable.StuckMatrix.HasValue)
                    {
                        if (vector2.HasValue && surfaceHeight.HasValue)
                        {
                            var num19 = surfaceHeight.Value - pickable.Position.Y;
                            var num20 = MathUtils.Saturate(3f * num19);
                            pickable.Velocity.X += 4f * dt * (vector2.Value.X - pickable.Velocity.X);
                            pickable.Velocity.Y -= 10f * dt;
                            pickable.Velocity.Y += 10f * (1f / block.Density * num20) * dt;
                            pickable.Velocity.Z += 4f * dt * (vector2.Value.Y - pickable.Velocity.Z);
                            pickable.Velocity.Y *= num2;
                        }
                        else
                        {
                            pickable.Velocity.Y -= 10f * dt;
                            pickable.Velocity *= num;
                        }
                    }
                }

                pickable.Position = vector;
            }
        }

        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        foreach (var item in PickablesToRemove)
        {
            _pickables.Remove(item);
            PickableRemoved?.Invoke(item);
            UseMaps.Remove(item.Id);
            //服务器广播pickable
            CommonLib.Net.QueuePackage(new PickablePackage(item, PickablePackage.PickType.Delete));
        }

        PickablesToRemove.Clear();
    }

    public event Action<Pickable>? PickableAdded;
    public event Action<Pickable>? PickableRemoved;

    public Pickable? AddPickable(int value, int count, Vector3 position, Vector3? velocity, Matrix? stuckMatrix)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return null;
        }

        //服务器广播pickable
        var pickable = CreatePickable(null, value, count, position, velocity, stuckMatrix);
        pickable.NetToRemove = true;
        CommonLib.Net.QueuePackage(new PickablePackage(pickable, PickablePackage.PickType.Create));
        return pickable;
    }

    public Pickable CreatePickable(ushort? id, int value, int count, Vector3 position, Vector3? velocity,
        Matrix? stuckMatrix)
    {
        var pickable = new Pickable
        {
            Id = id ?? FindAvailableId(),
            Value = value,
            Count = count,
            Position = position,
            StuckMatrix = stuckMatrix,
            CreationTime = _subsystemGameInfo.TotalElapsedGameTime
        };

        if (velocity.HasValue)
        {
            pickable.Velocity = velocity.Value;
        }
        else if (Terrain.ExtractContents(value) == ExperienceBlock.Index)
        {
            var vector = _random.Vector2(1.5f, 2f);
            pickable.Velocity = new Vector3(vector.X, 3f, vector.Y);
        }
        else
        {
            pickable.Velocity = new Vector3(_random.Float(-0.5f, 0.5f), _random.Float(1f, 1.2f),
                _random.Float(-0.5f, 0.5f));
        }

        PickableAdded?.Invoke(pickable);
        _pickables.Add(pickable);
        return pickable;
    }

    private ushort FindAvailableId()
    {
        for (ushort i = 1; i < ushort.MaxValue; i++)
        {
            if (!UseMaps.Contains(i))
            {
                UseMaps.Add(i);
                return i;
            }
        }

        return 0;
    }

    private void OnPlayerGetPickable(Pickable pickable, ComponentPlayer tmpPlayer, Vector3 positionFix, float distance)
    {
        var flag = Terrain.ExtractContents(pickable.Value) == 248;
        var inventory = tmpPlayer.ComponentMiner.Inventory;
        if (!flag && ComponentInventoryBase.FindAcquireSlotForItem(inventory, pickable.Value) < 0)
        {
            return;
        }

        if (distance < 1f)
        {
            if (flag)
            {
                tmpPlayer.ComponentLevel.AddExperience(pickable.Count, true);
                pickable.ToRemove = true;
            }
            else
            {
                pickable.Count = ComponentInventoryBase.AcquireItems(inventory, pickable.Value, pickable.Count);
                if (pickable.Count != 0 && CommonLib.WorkType != WorkType.Client)
                {
                    return;
                }

                pickable.ToRemove = true;
                pickable.PlaySound = true;
                PlayPickableCollectedSound(pickable);
            }
        }
        else if (!pickable.StuckMatrix.HasValue)
        {
            pickable.FlyToPosition =
                positionFix + 0.1f * MathUtils.Sqrt(distance) * tmpPlayer.ComponentBody.Velocity;
            CommonLib.Net.QueuePackage(new PickablePackage(pickable, PickablePackage.PickType.SetFlyToPosition));
        }
    }

    public void PlayPickableCollectedSound(Pickable pickable)
    {
        _subsystemAudio.PlaySound("Audio/PickableCollected", 0.7f, -0.4f, pickable.Position, 2f, false);
    }

    public bool PickableAction(ushort id, Action<Pickable> action, bool requestSync = true)
    {
        var flag = false;
        foreach (var pickable in _pickables)
        {
            if (pickable.Id == id)
            {
                action.Invoke(pickable);
                flag = true;
                break;
            }
        }

        if (!flag && CommonLib.WorkType == WorkType.Client && requestSync)
        {
            CommonLib.Net.QueuePackage(new PickablePackage(new Pickable { Id = id },
                PickablePackage.PickType.RequestSync));
        }

        return flag;
    }


    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true)!;
        _subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true)!;
        _subsystemFireBlockBehavior = Project.FindSubsystem<SubsystemFireBlockBehavior>(true)!;
        _subsystemFluidBlockBehavior = Project.FindSubsystem<SubsystemFluidBlockBehavior>(true)!;
        foreach (ValuesDictionary item in valuesDictionary.GetValue<ValuesDictionary>("Pickables").Values
                     .Where(v => v is ValuesDictionary))
        {
            var pickable = new Pickable();
            pickable.Id = item.GetValue("Id", pickable.Id);
            pickable.Value = item.GetValue("Value", pickable.Value);
            pickable.Count = item.GetValue("Count", pickable.Count);
            pickable.Position = item.GetValue("Position", pickable.Position);
            pickable.Velocity = item.GetValue("Velocity", pickable.Velocity);
            pickable.CreationTime = item.GetValue("CreationTime", pickable.CreationTime);
            if (pickable.Id == 0)
            {
                pickable.Id = FindAvailableId();
            }
            else
            {
                UseMaps.Add(pickable.Id);
            }

            if (item.ContainsKey("StuckMatrix"))
            {
                pickable.StuckMatrix = item.GetValue("StuckMatrix", pickable.StuckMatrix);
            }

            _pickables.Add(pickable);
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("Pickables", valuesDictionary2);
        var num = 0;
        foreach (var pickable in _pickables)
        {
            var valuesDictionary3 = new ValuesDictionary();
            valuesDictionary2.SetValue(num.ToString(), valuesDictionary3);
            valuesDictionary3.SetValue("Id", pickable.Id);
            valuesDictionary3.SetValue("Value", pickable.Value);
            valuesDictionary3.SetValue("Count", pickable.Count);
            valuesDictionary3.SetValue("Position", pickable.Position);
            valuesDictionary3.SetValue("Velocity", pickable.Velocity);
            valuesDictionary3.SetValue("CreationTime", pickable.CreationTime);
            if (pickable.StuckMatrix.HasValue)
            {
                valuesDictionary3.SetValue("StuckMatrix", pickable.StuckMatrix.Value);
            }

            num++;
        }
    }
}
