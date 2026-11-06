using System.Globalization;

using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemExplosivesBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    private readonly Dictionary<Point3, ExplosiveData> _explosiveDataByPoint = new();

    private readonly Random _random = new();

    private SubsystemExplosions _subsystemExplosions = null!;

    private SubsystemFireBlockBehavior _subsystemFireBlockBehavior = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private readonly HashSet<Point3> _replicaFusePoints = [];

    private SubsystemNetworkInterest? _interest;

    private bool _disposed;

    public event Action<Point3>? FuseRemoved;

    public IReadOnlyCollection<Point3> ActiveFusePoints => WorldRuntimeContext.For(Project).IsAuthority
        ? _explosiveDataByPoint.Keys
        : _replicaFusePoints;

    public override ResourceId[] HandledBlocks => [];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_disposed || !WorldRuntimeContext.For(Project).IsAuthority) { return; }

        foreach (var explosive in _explosiveDataByPoint.Values.ToArray())
        {
            var point = explosive.Point;
            var cellValue = _subsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
            explosive.TimeToExplosion -= dt;
            if (explosive.TimeToExplosion <= 0f)
            {
                _subsystemExplosions.TryExplodeBlock(point.X, point.Y, point.Z, cellValue, explosive.Miner);
            }
        }
    }

    public bool IgniteFuse(int x, int y, int z, CharacterData? miner = null)
    {
        var cellContents = _subsystemTerrain.Terrain.GetCellContents(x, y, z);
        switch (WorldContentBinding.For(Project).Catalog.Blocks[cellContents])
        {
            case GunpowderKegBlock:
                //延时爆炸
                if (WorldRuntimeContext.For(Project).IsAuthority)
                {
                    AddExplosive(new Point3(x, y, z), _random.Float(6f, 7f), miner);
                }

                return true;
            case DetonatorBlock:
                //延时爆炸
                if (WorldRuntimeContext.For(Project).IsAuthority)
                {
                    AddExplosive(new Point3(x, y, z), _random.Float(0.8f, 1.2f), miner);
                }

                return true;
            default:
                return false;
        }
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority && _subsystemFireBlockBehavior.IsCellOnFire(x, y, z))
        {
            IgniteFuse(x, y, z);
        }
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        var point = new Point3(x, y, z);
        RemoveExplosive(point);
    }

    public override void OnChunkDiscarding(TerrainChunk chunk)
    {
        var list = new List<Point3>();
        foreach (var key in ActiveFusePoints)
        {
            if (key.X >= chunk.Origin.X &&
                key.X < chunk.Origin.X + 16 &&
                key.Z >= chunk.Origin.Y &&
                key.Z < chunk.Origin.Y + 16)
            {
                list.Add(key);
            }
        }

        foreach (var item in list)
        {
            RemoveExplosive(item);
        }
    }

    public override void OnExplosion(int value, int x, int y, int z, float damage)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        var num = Terrain.ExtractContents(value);
        var block = WorldContentBinding.For(Project).Catalog.Blocks[num];
        if (block.GetExplosionPressure(value) > 0f &&
            MathUtils.Saturate(damage / block.ExplosionResilience) > 0.01f &&
            SubsystemExplosions.SharedRandom.Float(0f, 1f) < 0.5f)
        {
            IgniteFuse(x, y, z);
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true)!;
        _subsystemFireBlockBehavior = Project.FindSubsystem<SubsystemFireBlockBehavior>(true)!;
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        _interest = Project.FindSubsystem<SubsystemNetworkInterest>(true)!;
        _interest.ChunkEntered += SendChunkState;
        foreach (ValuesDictionary entry in valuesDictionary.GetValue<ValuesDictionary>("Explosives").Values)
        {
            AddExplosive(entry.GetValue<Point3>("Point"), entry.GetValue<float>("TimeToExplosion"), publish: false);
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        base.Save(valuesDictionary);
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        var num = 0;
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("Explosives", valuesDictionary2);
        foreach (var value in _explosiveDataByPoint.Values)
        {
            var valuesDictionary3 = new ValuesDictionary();
            valuesDictionary2.SetValue(num++.ToString(CultureInfo.InvariantCulture), valuesDictionary3);
            valuesDictionary3.SetValue("Point", value.Point);
            valuesDictionary3.SetValue("TimeToExplosion", value.TimeToExplosion);
        }
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_interest is not null) { _interest.ChunkEntered -= SendChunkState; }

        _interest = null;
        _explosiveDataByPoint.Clear();
        _replicaFusePoints.Clear();
        FuseRemoved = null;
        base.Dispose();
    }

    private void SendChunkState(WorldConnection client, Point2 chunk)
    {
        foreach (var point in _explosiveDataByPoint.Keys.Where(point =>
                     point.X >> 4 == chunk.X && point.Z >> 4 == chunk.Y
                 ))
        {
            NetworkSender.SendTo(client, new BlockFuseAddedPackage { Point = point });
        }
    }

    public void ApplyReplicaFuseAdded(Point3 point)
    {
        if (!_disposed && !WorldRuntimeContext.For(Project).IsAuthority) { _replicaFusePoints.Add(point); }
    }

    public void ApplyReplicaFuseRemoved(Point3 point)
    {
        if (!_disposed && !WorldRuntimeContext.For(Project).IsAuthority) { RemoveExplosive(point); }
    }

    public void ClearReplicaChunkState(Point2 chunk)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority) { return; }

        foreach (var point in _replicaFusePoints.Where(point => point.X >> 4 == chunk.X && point.Z >> 4 == chunk.Y)
                     .ToArray())
        {
            RemoveExplosive(point);
        }
    }

    private void AddExplosive(Point3 point, float timeToExplosion, CharacterData? miner = null, bool publish = true)
    {
        if (_explosiveDataByPoint.ContainsKey(point))
        {
            return;
        }

        var explosiveData = new ExplosiveData
        {
            Point = point,
            Miner = miner,
            TimeToExplosion = timeToExplosion
        };
        _explosiveDataByPoint.Add(point, explosiveData);
        if (publish)
        {
            NetworkSender.SendToChunkObservers(
                Project,
                new Point2(point.X >> 4, point.Z >> 4),
                new BlockFuseAddedPackage { Point = point }
            );
        }
    }

    private void RemoveExplosive(Point3 point)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            if (!_explosiveDataByPoint.Remove(point)) { return; }

            NetworkSender.SendToChunkObservers(
                Project,
                new Point2(point.X >> 4, point.Z >> 4),
                new BlockFuseRemovedPackage { Point = point }
            );
        }
        else if (!_replicaFusePoints.Remove(point)) { return; }

        FuseRemoved?.Invoke(point);
    }

    private class ExplosiveData
    {
        public CharacterData? Miner;

        public Point3 Point;

        public float TimeToExplosion;
    }
}
