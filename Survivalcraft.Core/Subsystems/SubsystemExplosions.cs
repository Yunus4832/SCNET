using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemExplosions : Subsystem, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    public static readonly Random SharedRandom = new();

    private readonly Dictionary<Point2, List<(Point3, float)>> _explosionCells = new();

    private readonly Dictionary<Projectile, bool> _generatedProjectiles = new();

    private SparseSpatialArray<float> _pressureByPoint = null!;

    private int _projectilesCount;

    private readonly List<ExplosionData> _queuedExplosions = [];

    private SubsystemBlockBehaviors _subsystemBlockBehaviors = null!;

    private SubsystemBodies _subsystemBodies = null!;

    private SubsystemFireBlockBehavior _subsystemFireBlockBehavior = null!;

    private SubsystemNoise _subsystemNoise = null!;

    private SubsystemPickables _subsystemPickables = null!;

    private SubsystemProjectiles _subsystemProjectiles = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SparseSpatialArray<SurroundingPressurePoint> _surroundingPressureByPoint = null!;

    public bool ShowExplosionPressure;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public virtual void Update(float dt)
    {
        if (_queuedExplosions.Count <= 0)
        {
            return;
        }

        var x = _queuedExplosions[0].X;
        var y = _queuedExplosions[0].Y;
        var z = _queuedExplosions[0].Z;
        _pressureByPoint = new SparseSpatialArray<float>(x, y, z, 0f);
        _surroundingPressureByPoint = new SparseSpatialArray<SurroundingPressurePoint>(
            x,
            y,
            z,
            new SurroundingPressurePoint
            {
                IsIncendiary = false,
                Pressure = 0f
            }
        );
        _projectilesCount = 0;
        _generatedProjectiles.Clear();
        var flag = false;
        var num = 0;
        while (num < _queuedExplosions.Count)
        {
            var explosionData = _queuedExplosions[num];
            if (MathUtils.Abs(explosionData.X - x) <= 4 &&
                MathUtils.Abs(explosionData.Y - y) <= 4 &&
                MathUtils.Abs(explosionData.Z - z) <= 4)
            {
                _queuedExplosions.RemoveAt(num);
                SimulateExplosion(
                    explosionData.X,
                    explosionData.Y,
                    explosionData.Z,
                    explosionData.Pressure,
                    explosionData.IsIncendiary,
                    explosionData.Miner
                );
                flag |= !explosionData.NoExplosionSound;
            }
            else
            {
                num++;
            }
        }

        PostprocessExplosions(flag);
        if (!ShowExplosionPressure)
        {
            _pressureByPoint = null!;
            _surroundingPressureByPoint = null!;
        }

        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        SendExplosionCellsToObservers();
        _explosionCells.Clear();
    }


    public virtual bool TryExplodeBlock(
        int x,
        int y,
        int z,
        int value,
        CharacterData? miner = null
    )
    {
        return WorldRuntimeContext.For(Project).IsAuthority && TryNetExplodeBlock(x, y, z, value, miner);
    }

    public virtual bool TryNetExplodeBlock(
        int x,
        int y,
        int z,
        int value,
        CharacterData? miner = null
    )
    {
        var num = Terrain.ExtractContents(value);
        var obj = WorldContentBinding.For(Project).Catalog.Blocks[num];
        var explosionPressure = obj.GetExplosionPressure(value);
        var explosionIncendiary = obj.GetExplosionIncendiary(value);
        if (!(explosionPressure > 0f))
        {
            return false;
        }

        AddExplosion(x, y, z, explosionPressure, explosionIncendiary, false, miner);
        return true;
    }

    public virtual void AddExplosion(
        int x,
        int y,
        int z,
        float pressure,
        bool isIncendiary,
        bool noExplosionSound,
        CharacterData? miner = null
    )
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        if (pressure > 0f)
        {
            _queuedExplosions.Add(
                new ExplosionData
                {
                    X = x,
                    Y = y,
                    Z = z,
                    Pressure = pressure,
                    IsIncendiary = isIncendiary,
                    NoExplosionSound = noExplosionSound,
                    Miner = miner
                }
            );
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true)!;
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        _subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        _subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true)!;
        _subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true)!;
        _subsystemFireBlockBehavior = Project.FindSubsystem<SubsystemFireBlockBehavior>(true)!;
    }

    public virtual void SimulateExplosion(
        int x,
        int y,
        int z,
        float pressure,
        bool isIncendiary,
        CharacterData? miner = null
    )
    {
        var explosionPointValue = _subsystemTerrain.Terrain.GetCellValue(x, y, z);
        var num = MathUtils.Max(0.13f * MathUtils.Pow(pressure, 0.5f), 1f);
        _subsystemTerrain.ChangeCell(x, y, z, 0);
        var processed = new SparseSpatialArray<bool>(x, y, z, true);
        var list = new List<ProcessPoint>();
        var list2 = new List<ProcessPoint>();
        var list3 = new List<ProcessPoint>();
        TryAddPoint(x, y, z, -1, pressure, isIncendiary, list, processed, miner);
        var num2 = 0;
        var num3 = 0;
        if (Terrain.ExtractContents(explosionPointValue) != 0)
        {
        }

        while (list.Count > 0 || list2.Count > 0)
        {
            num2 += list.Count;
            num3++;
            var num4 = 5f * MathUtils.Max(num3 - 7, 0);
            var num5 = pressure / (MathUtils.Pow(num2, 0.66f) + num4);
            if (num5 >= num)
            {
                foreach (var item in list)
                {
                    var num6 = _pressureByPoint.Get(item.X, item.Y, item.Z);
                    var num7 = num5 + num6;
                    _pressureByPoint.Set(item.X, item.Y, item.Z, num7);
                    if (item.Axis == 0)
                    {
                        TryAddPoint(item.X - 1, item.Y, item.Z, 0, num7, isIncendiary, list3, processed, miner);
                        TryAddPoint(item.X + 1, item.Y, item.Z, 0, num7, isIncendiary, list3, processed, miner);
                        TryAddPoint(item.X, item.Y - 1, item.Z, -1, num7, isIncendiary, list2, processed, miner);
                        TryAddPoint(item.X, item.Y + 1, item.Z, -1, num7, isIncendiary, list2, processed, miner);
                        TryAddPoint(item.X, item.Y, item.Z - 1, -1, num7, isIncendiary, list2, processed, miner);
                        TryAddPoint(item.X, item.Y, item.Z + 1, -1, num7, isIncendiary, list2, processed, miner);
                    }
                    else if (item.Axis == 1)
                    {
                        TryAddPoint(item.X - 1, item.Y, item.Z, -1, num7, isIncendiary, list2, processed, miner);
                        TryAddPoint(item.X + 1, item.Y, item.Z, -1, num7, isIncendiary, list2, processed, miner);
                        TryAddPoint(item.X, item.Y - 1, item.Z, 1, num7, isIncendiary, list3, processed, miner);
                        TryAddPoint(item.X, item.Y + 1, item.Z, 1, num7, isIncendiary, list3, processed, miner);
                        TryAddPoint(item.X, item.Y, item.Z - 1, -1, num7, isIncendiary, list2, processed, miner);
                        TryAddPoint(item.X, item.Y, item.Z + 1, -1, num7, isIncendiary, list2, processed, miner);
                    }
                    else if (item.Axis == 2)
                    {
                        TryAddPoint(item.X - 1, item.Y, item.Z, -1, num7, isIncendiary, list2, processed, miner);
                        TryAddPoint(item.X + 1, item.Y, item.Z, -1, num7, isIncendiary, list2, processed, miner);
                        TryAddPoint(item.X, item.Y - 1, item.Z, -1, num7, isIncendiary, list2, processed, miner);
                        TryAddPoint(item.X, item.Y + 1, item.Z, -1, num7, isIncendiary, list2, processed, miner);
                        TryAddPoint(item.X, item.Y, item.Z - 1, 2, num7, isIncendiary, list3, processed, miner);
                        TryAddPoint(item.X, item.Y, item.Z + 1, 2, num7, isIncendiary, list3, processed, miner);
                    }
                    else
                    {
                        TryAddPoint(item.X - 1, item.Y, item.Z, 0, num7, isIncendiary, list3, processed, miner);
                        TryAddPoint(item.X + 1, item.Y, item.Z, 0, num7, isIncendiary, list3, processed, miner);
                        TryAddPoint(item.X, item.Y - 1, item.Z, 1, num7, isIncendiary, list3, processed, miner);
                        TryAddPoint(item.X, item.Y + 1, item.Z, 1, num7, isIncendiary, list3, processed, miner);
                        TryAddPoint(item.X, item.Y, item.Z - 1, 2, num7, isIncendiary, list3, processed, miner);
                        TryAddPoint(item.X, item.Y, item.Z + 1, 2, num7, isIncendiary, list3, processed, miner);
                    }
                }
            }

            var list4 = list;
            list4.Clear();
            list = list2;
            list2 = list3;
            list3 = list4;
        }
    }

    public virtual void TryAddPoint(
        int x,
        int y,
        int z,
        int axis,
        float currentPressure,
        bool isIncendiary,
        List<ProcessPoint> toProcess,
        SparseSpatialArray<bool> processed,
        CharacterData? characterData = null
    )
    {
        if (processed.Get(x, y, z))
        {
            return;
        }

        var gameplay = WorldRuleHooks.For(Project);
        if (gameplay is { HasExplosionPointProcessingHandlers: true })
        {
            var processing = new ExplosionPointProcessingContext(this, new Point3(x, y, z), characterData);
            gameplay.Invoke(processing);
            if (processing.Cancel)
            {
                return;
            }
        }

        var cellValue = _subsystemTerrain.Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractContents(cellValue);
        if (num != 0)
        {
            var num2 = (int)(MathUtils.Hash((uint)(x + 913 * y + 217546 * z)) % 100u);
            var num3 = MathUtils.Lerp(1f, 2f, num2 / 100f);
            if (num2 % 8 == 0)
            {
                num3 *= 3f;
            }

            var block = WorldContentBinding.For(Project).Catalog.Blocks[num];
            var num4 = _pressureByPoint.Get(x - 1, y, z) +
                       _pressureByPoint.Get(x + 1, y, z) +
                       _pressureByPoint.Get(x, y - 1, z) +
                       _pressureByPoint.Get(x, y + 1, z) +
                       _pressureByPoint.Get(x, y, z - 1) +
                       _pressureByPoint.Get(x, y, z + 1);
            var num5 = MathUtils.Max(block.ExplosionResilience * num3, 1f);
            var num6 = num4 / num5;
            if (num6 > 1f)
            {
                var newValue = Terrain.MakeBlockValue(0);
                _subsystemTerrain.DestroyCell(
                    0,
                    x,
                    y,
                    z,
                    newValue,
                    true,
                    true,
                    characterData?.ComponentCharacter?.ComponentMiner
                );
                var flag = false;
                var probability = num6 > 5f ? 0.95f : 0.75f;
                if (SharedRandom.Bool(probability))
                {
                    //爆炸方块
                    flag = TryNetExplodeBlock(x, y, z, cellValue);
                }

                if (!flag)
                {
                    CalculateImpulseAndDamage(
                        new Vector3(x + 0.5f, y + 0.5f, z + 0.5f),
                        60f,
                        2f * num4,
                        out var impulse,
                        out _
                    );
                    var flag2 = false;
                    var list = new List<BlockDropValue>();
                    block.GetDropValues(_subsystemTerrain, cellValue, newValue, 0, list, out _);
                    if (list.Count == 0)
                    {
                        list.Add(
                            new BlockDropValue
                            {
                                Value = cellValue,
                                Count = 1
                            }
                        );
                        flag2 = true;
                    }

                    foreach (var item in list)
                    {
                        var num7 = Terrain.ExtractContents(item.Value);
                        var block2 = WorldContentBinding.For(Project).Catalog.Blocks[num7];
                        if (!(WorldContentBinding.For(Project).Catalog.Blocks[num7] is FluidBlock))
                        {
                            var num8 = _projectilesCount < 40 || block2.ExplosionKeepsPickable ? 1f :
                                _projectilesCount < 60 ? 0.5f :
                                _projectilesCount >= 80 ? 0.125f : 0.25f;
                            if (SharedRandom.Float(0f, 1f) < num8)
                            {
                                var velocity = impulse + SharedRandom.Vector3(0.05f * impulse.Length());
                                if (_projectilesCount >= 1)
                                {
                                    velocity *= SharedRandom.Float(0.5f, 1f);
                                    velocity += SharedRandom.Vector3(0.2f * velocity.Length());
                                }

                                var num9 = flag2 ? 0f :
                                    block2.ExplosionKeepsPickable ? 1f :
                                    MathUtils.Lerp(1f, 0f, _projectilesCount / 25f);
                                //添加抛射物
                                var projectile = _subsystemProjectiles.AddProjectile(
                                    item.Value,
                                    new Vector3(x + 0.5f, y + 0.5f, z + 0.5f),
                                    velocity,
                                    SharedRandom.Vector3(0f, 20f),
                                    null
                                );
                                if (projectile != null)
                                {
                                    projectile.ProjectileStoppedAction = !(SharedRandom.Float(0f, 1f) < num9)
                                        ? ProjectileStoppedAction.Disappear
                                        : ProjectileStoppedAction.TurnIntoPickable;
                                    if (SharedRandom.Float(0f, 1f) < 0.5f && _projectilesCount < 35)
                                    {
                                        var num10 = num4 > 60f
                                            ? SharedRandom.Float(3f, 7f)
                                            : SharedRandom.Float(1f, 3f);
                                        if (isIncendiary)
                                        {
                                            num10 += 10f;
                                        }

                                        _subsystemProjectiles.SetSmokeTrail(
                                            projectile,
                                            SharedRandom.Float(0.75f, 1.5f),
                                            num10,
                                            isIncendiary ? new Color(255, 140, 192) : Color.White
                                        );

                                        projectile.IsIncendiary = isIncendiary;
                                    }

                                    _generatedProjectiles.Add(projectile, true);
                                }

                                _projectilesCount++;
                            }
                        }
                    }
                }
            }
            else
            {
                _surroundingPressureByPoint.Set(
                    x,
                    y,
                    z,
                    new SurroundingPressurePoint
                    {
                        Pressure = num4,
                        IsIncendiary = isIncendiary
                    }
                );
                if (block.Collidable)
                {
                    return;
                }
            }
        }

        toProcess.Add(
            new ProcessPoint
            {
                X = x,
                Y = y,
                Z = z,
                Axis = axis
            }
        );
        processed.Set(x, y, z, true);
    }

    public virtual void PostprocessExplosions(bool playExplosionSound)
    {
        var pressures = _pressureByPoint.ToDictionary();
        var point = ExplosionAcoustics.SelectOrigin(pressures);
        var num2 = 0f;
        foreach (var item in pressures)
        {
            num2 += item.Value;
            var num4 = 0.001f * MathUtils.Pow(num2, 0.5f);
            var num5 = MathUtils.Saturate(item.Value / 15f - num4) * SharedRandom.Float(0.2f, 1f);
            if (!(num5 > 0.1f))
            {
                continue;
            }

            if (WorldRuntimeContext.For(Project).IsAuthority)
            {
                var chunk = new Point2(item.Key.X >> 4, item.Key.Z >> 4);
                if (!_explosionCells.ContainsKey(chunk))
                {
                    _explosionCells.Add(chunk, []);
                }

                _explosionCells[chunk].Add((item.Key, num5));
            }
        }

        foreach (var item2 in _surroundingPressureByPoint.ToDictionary())
        {
            var cellValue = _subsystemTerrain.Terrain.GetCellValue(item2.Key.X, item2.Key.Y, item2.Key.Z);
            var num6 = Terrain.ExtractContents(cellValue);
            var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(num6);
            if (blockBehaviors.Length != 0)
            {
                foreach (var behavior in blockBehaviors)
                {
                    behavior.OnExplosion(
                        cellValue,
                        item2.Key.X,
                        item2.Key.Y,
                        item2.Key.Z,
                        item2.Value.Pressure
                    );
                }
            }

            var probability = item2.Value.IsIncendiary ? 0.5f : 0.2f;
            var block = WorldContentBinding.For(Project).Catalog.Blocks[num6];
            //方块燃烧
            if (block.FireDuration > 0f &&
                item2.Value.Pressure / block.ExplosionResilience > 0.2f &&
                SharedRandom.Bool(probability))
            {
                var f = item2.Value.IsIncendiary ? 1f : 0.3f;
                _subsystemFireBlockBehavior.SetCellOnFire(item2.Key.X, item2.Key.Y, item2.Key.Z, f);
            }
        }

        foreach (var body in _subsystemBodies.Bodies)
        {
            CalculateImpulseAndDamage(body, null, out var impulse, out var damage);
            impulse *= SharedRandom.Float(0.5f, 1.5f);
            damage *= SharedRandom.Float(0.5f, 1.5f);
            body.ApplyImpulse(impulse);
            //生物燃烧
            body.Entity.FindComponent<ComponentHealth>()
                ?.Injure(damage, null, false, new LocalizedText(GetType().Name, "0", "0"));
            body.Entity.FindComponent<ComponentDamage>()?.Damage(damage);
            var componentOnFire = body.Entity.FindComponent<ComponentOnFire>();
            if (componentOnFire != null && SharedRandom.Float(0f, 1f) < MathUtils.Min(damage - 0.1f, 0.5f))
            {
                var duration = SharedRandom.Float(6f, 8f);
                componentOnFire.SetOnFire(null, duration);
            }
        }

        foreach (var pickable in _subsystemPickables.Pickables)
        {
            var block2 = WorldContentBinding.For(Project).Catalog
                .Blocks[Terrain.ExtractContents(pickable.Value)];
            CalculateImpulseAndDamage(
                pickable.Position + new Vector3(0f, 0.5f, 0f),
                20f,
                null,
                out var impulse2,
                out var damage2
            );
            if (damage2 / block2.ExplosionResilience > 0.1f)
            {
                TryNetExplodeBlock(
                    Terrain.ToCell(pickable.Position.X),
                    Terrain.ToCell(pickable.Position.Y),
                    Terrain.ToCell(pickable.Position.Z),
                    pickable.Value
                );
                pickable.ToRemove = true;
            }
            else
            {
                var vector = (impulse2 + new Vector3(0f, 0.1f * impulse2.Length(), 0f)) * SharedRandom.Float(0.75f, 1f);
                if (vector.Length() > 10f)
                {
                    var projectile = _subsystemProjectiles.AddProjectile(
                        pickable.Value,
                        pickable.Position,
                        pickable.Velocity + vector,
                        SharedRandom.Vector3(0f, 20f),
                        null
                    );
                    if (SharedRandom.Float(0f, 1f) < 0.33f)
                    {
                        if (projectile != null)
                        {
                            _subsystemProjectiles.SetSmokeTrail(
                                projectile,
                                SharedRandom.Float(0.75f, 1.5f),
                                SharedRandom.Float(1f, 6f),
                                Color.White
                            );
                        }
                    }

                    pickable.ToRemove = true;
                }
                else
                {
                    pickable.Velocity += vector;
                }
            }
        }

        foreach (var projectile2 in _subsystemProjectiles.Projectiles)
        {
            if (!_generatedProjectiles.ContainsKey(projectile2))
            {
                CalculateImpulseAndDamage(
                    projectile2.Position + new Vector3(0f, 0.5f, 0f),
                    20f,
                    null,
                    out var impulse3,
                    out _
                );
                projectile2.Velocity += (impulse3 + new Vector3(0f, 0.1f * impulse3.Length(), 0f)) *
                                        SharedRandom.Float(0.75f, 1f);
            }
        }

        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        if (point is not { } origin) { return; }

        var position = new Vector3(origin) + new Vector3(.5f);
        var profile = ExplosionAcoustics.ForPressure(num2);
        if (profile.NoiseRadius > 0f) { _subsystemNoise.MakeNoise(position, 1f, profile.NoiseRadius); }

        // Preserve the authority's existing acoustic random draw independently of presentation availability.
        var pitch = playExplosionSound && num2 > 0f
            ? SharedRandom.Float(-profile.PitchVariation, profile.PitchVariation)
            : 0f;
        if (playExplosionSound)
        {
            var interest = Project.FindSubsystem<SubsystemNetworkInterest>(true)!;
            var audibleRange = profile.MinimumDistance;
            var observers = interest.GetObserversWithin(position.XZ, audibleRange).ToArray();
            if (observers.Length > 0)
            {
                NetworkSender.Send(
                    Project,
                    new ExplosionSoundPackage { Position = position, Level = num2, Pitch = pitch },
                    PackageAudience.To(observers)
                );
            }
        }
    }

    private void SendExplosionCellsToObservers()
    {
        if (_explosionCells.Count == 0)
        {
            return;
        }

        var interest = Project.FindSubsystem<SubsystemNetworkInterest>(true)!;
        var cellsByClient = new Dictionary<WorldConnection, Dictionary<Point2, List<(Point3, float)>>>(
            ReferenceEqualityComparer.Instance
        );
        foreach (var (chunk, cells) in _explosionCells)
        {
            foreach (var client in interest.GetChunkObservers(chunk))
            {
                if (!cellsByClient.TryGetValue(client, out var clientCells))
                {
                    clientCells = [];
                    cellsByClient.Add(client, clientCells);
                }

                clientCells.Add(chunk, cells);
            }
        }

        foreach (var (client, cells) in cellsByClient)
        {
            NetworkSender.Send(
                Project,
                ExplosionPackageBuilder.Capture(cells),
                PackageAudience.To(client)
            );
        }
    }


    public virtual void CalculateImpulseAndDamage(
        ComponentBody componentBody,
        float? obstaclePressure,
        out Vector3 impulse,
        out float damage
    )
    {
        CalculateImpulseAndDamage(
            0.5f * (componentBody.BoundingBox.Min + componentBody.BoundingBox.Max),
            componentBody.Mass,
            obstaclePressure,
            out impulse,
            out damage
        );
    }

    public virtual void CalculateImpulseAndDamage(
        Vector3 position,
        float mass,
        float? obstaclePressure,
        out Vector3 impulse,
        out float damage
    )
    {
        var point = Terrain.ToCell(position);
        obstaclePressure ??= _pressureByPoint.Get(point.X, point.Y, point.Z);

        var num = 0f;
        var zero = Vector3.Zero;
        for (var i = -1; i <= 1; i++)
        {
            for (var j = -1; j <= 1; j++)
            {
                for (var k = -1; k <= 1; k++)
                {
                    var num2 = point.X + i;
                    var num3 = point.Y + j;
                    var num4 = point.Z + k;
                    var num5 = _subsystemTerrain.Terrain.GetCellContents(num2, num3, num4) != 0
                        ? obstaclePressure.Value
                        : _pressureByPoint.Get(num2, num3, num4);
                    if (i != 0 || j != 0 || k != 0)
                    {
                        zero += num5 * Vector3.Normalize(new Vector3(point.X - num2, point.Y - num3, point.Z - num4));
                    }

                    num += num5;
                }
            }
        }

        var num6 = MathUtils.Max(MathUtils.Pow(mass, 0.5f), 1f);
        impulse = 5.5555553f * Vector3.Normalize(zero) * MathUtils.Sqrt(zero.Length()) / num6;
        damage = 2.59259248f * MathUtils.Sqrt(num) / num6;
    }

    public class SparseSpatialArray<T>(int centerX, int centerY, int centerZ, T outside)
    {
        public const int Bits1 = 4;

        public const int Bits2 = 4;

        public const int Mask1 = 15;

        public const int Mask2 = 15;

        public const int Diameter = 256;

        private readonly T[]?[] _data = new T[4096][];

        private readonly int _originX = centerX - 128;

        private readonly int _originY = centerY - 128;

        private readonly int _originZ = centerZ - 128;

        public T? Get(int x, int y, int z)
        {
            x -= _originX;
            y -= _originY;
            z -= _originZ;
            if (x is < 0 or >= 256 || y is < 0 or >= 256 || z is < 0 or >= 256)
            {
                return outside;
            }

            var num = x >> 4;
            var num2 = y >> 4;
            var num3 = z >> 4;
            var num4 = num + (num2 << 4) + (num3 << 4 << 4);
            var array = _data[num4];
            if (array != null)
            {
                var num5 = x & 0xF;
                var num6 = y & 0xF;
                var num7 = z & 0xF;
                var num8 = num5 + (num6 << 4) + (num7 << 4 << 4);
                return array[num8];
            }

            return default;
        }

        public void Set(int x, int y, int z, T value)
        {
            x -= _originX;
            y -= _originY;
            z -= _originZ;
            if (x is < 0 or >= 256 || y is < 0 or >= 256 || z is < 0 or >= 256)
            {
                return;
            }

            var num = x >> 4;
            var num2 = y >> 4;
            var num3 = z >> 4;
            var num4 = num + (num2 << 4) + (num3 << 4 << 4);
            var array = _data[num4];
            if (array == null)
            {
                array = new T[4096];
                _data[num4] = array;
            }

            var num5 = x & 0xF;
            var num6 = y & 0xF;
            var num7 = z & 0xF;
            var num8 = num5 + (num6 << 4) + (num7 << 4 << 4);
            array[num8] = value;
        }

        public void Clear()
        {
            for (var i = 0; i < _data.Length; i++)
            {
                _data[i] = null;
            }
        }

        public Dictionary<Point3, T> ToDictionary()
        {
            var dictionary = new Dictionary<Point3, T>();
            for (var i = 0; i < _data.Length; i++)
            {
                var array = _data[i];
                if (array == null)
                {
                    continue;
                }

                var num = _originX + ((i & 0xF) << 4);
                var num2 = _originY + (((i >> 4) & 0xF) << 4);
                var num3 = _originZ + (((i >> 8) & 0xF) << 4);
                for (var j = 0; j < array.Length; j++)
                {
                    if (!Equals(array[j], default(T)))
                    {
                        var num4 = j & 0xF;
                        var num5 = (j >> 4) & 0xF;
                        var num6 = (j >> 8) & 0xF;
                        dictionary.Add(new Point3(num + num4, num2 + num5, num3 + num6), array[j]);
                    }
                }
            }

            return dictionary;
        }
    }

    private struct ExplosionData
    {
        public int X;

        public int Y;

        public int Z;

        public float Pressure;

        public bool IsIncendiary;

        public bool NoExplosionSound;

        public CharacterData? Miner;
    }

    public struct ProcessPoint
    {
        public int X;

        public int Y;

        public int Z;

        public int Axis;
    }

    private struct SurroundingPressurePoint
    {
        public float Pressure;

        public bool IsIncendiary;
    }
}
