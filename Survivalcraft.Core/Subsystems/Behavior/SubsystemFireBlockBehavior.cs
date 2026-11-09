using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemFireBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    private readonly Dictionary<Point3, float> _expansionProbabilities = new();

    private readonly Dictionary<Point3, FireData> _fireData = new();

    private readonly DynamicArray<Point3> _firePointsCopy = new();

    private readonly Random _random = new();

    private readonly Dictionary<Point3, float> _toBurnAway = new();

    private readonly Dictionary<Point3, float> _toExpand = new();

    private int _copyIndex;

    private float _lastScanDuration;

    private double _lastScanTime;

    private float _remainderToScan;

    private SubsystemTime _subsystemTime = null!;

    private bool _subscribedToChunkInterest;

    public IReadOnlyCollection<Point3> FirePoints => _fireData.Keys;

    public bool ContainsFire(Point3 point) => _fireData.ContainsKey(point);

    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:fire")];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        if (_firePointsCopy.Count == 0)
        {
            _firePointsCopy.Count += _fireData.Count;
            _fireData.Keys.CopyTo(_firePointsCopy.Array, 0);
            _copyIndex = 0;
            _lastScanDuration = (float)(_subsystemTime.GameTime - _lastScanTime);
            _lastScanTime = _subsystemTime.GameTime;
        }

        if (_firePointsCopy.Count > 0)
        {
            var num = MathUtils.Min(1f * dt * _firePointsCopy.Count + _remainderToScan, 50f);
            var num2 = (int)num;
            _remainderToScan = num - num2;
            var num3 = MathUtils.Min(_copyIndex + num2, _firePointsCopy.Count);
            while (_copyIndex < num3)
            {
                if (_fireData.TryGetValue(_firePointsCopy.Array[_copyIndex], out var value))
                {
                    var x = value.Point.X;
                    var y = value.Point.Y;
                    var z = value.Point.Z;
                    var chunk = SubsystemTerrain.Terrain.GetChunkAtCell(x, z, false);
                    if (chunk is not { HasContents: true })
                    {
                        _copyIndex++;
                        continue;
                    }

                    if (SubsystemTerrain.Terrain.GetCellContents(x, y, z) != BuiltInBlockRegistry.GetIndex<FireBlock>())
                    {
                        RemoveFireNet(x, y, z);
                        _copyIndex++;
                        continue;
                    }

                    // Chunk baselines do not necessarily replay every historical neighbor change.
                    // Revalidate fuel on the authority so orphan fire cells cannot survive indefinitely.
                    if (WorldRuntimeContext.For(Project).IsAuthority && HasLoadedFuelNeighbors(x, z))
                    {
                        OnNeighborBlockChanged(x, y, z, x, y, z);
                        if (SubsystemTerrain.Terrain.GetCellContents(x, y, z) !=
                            BuiltInBlockRegistry.GetIndex<FireBlock>())
                        {
                            _copyIndex++;
                            continue;
                        }
                    }

                    var num4 = Terrain.ExtractData(SubsystemTerrain.Terrain.GetCellValue(x, y, z));
                    if ((num4 & 1) != 0)
                    {
                        value.Time0 -= _lastScanDuration;
                        if (value.Time0 <= 0f)
                        {
                            QueueBurnAway(x, y, z + 1, value.FireExpandability * 0.85f);
                        }

                        foreach (var expansionProbability in _expansionProbabilities)
                        {
                            if (_random.Float(0f, 1f) <
                                expansionProbability.Value *
                                _lastScanDuration *
                                value.FireExpandability)
                            {
                                _toExpand[
                                    new Point3(
                                        x + expansionProbability.Key.X,
                                        y + expansionProbability.Key.Y,
                                        z + 1 + expansionProbability.Key.Z
                                    )] = value.FireExpandability * 0.85f;
                            }
                        }
                    }

                    if ((num4 & 2) != 0)
                    {
                        value.Time1 -= _lastScanDuration;
                        if (value.Time1 <= 0f)
                        {
                            QueueBurnAway(x + 1, y, z, value.FireExpandability * 0.85f);
                        }

                        foreach (var expansionProbability2 in _expansionProbabilities)
                        {
                            if (_random.Float(0f, 1f) <
                                expansionProbability2.Value *
                                _lastScanDuration *
                                value.FireExpandability)
                            {
                                _toExpand[
                                    new Point3(
                                        x + 1 + expansionProbability2.Key.X,
                                        y + expansionProbability2.Key.Y,
                                        z + expansionProbability2.Key.Z
                                    )] = value.FireExpandability * 0.85f;
                            }
                        }
                    }

                    if ((num4 & 4) != 0)
                    {
                        value.Time2 -= _lastScanDuration;
                        if (value.Time2 <= 0f)
                        {
                            QueueBurnAway(x, y, z - 1, value.FireExpandability * 0.85f);
                        }

                        foreach (var expansionProbability3 in _expansionProbabilities)
                        {
                            if (_random.Float(0f, 1f) <
                                expansionProbability3.Value *
                                _lastScanDuration *
                                value.FireExpandability)
                            {
                                _toExpand[
                                    new Point3(
                                        x + expansionProbability3.Key.X,
                                        y + expansionProbability3.Key.Y,
                                        z - 1 + expansionProbability3.Key.Z
                                    )] = value.FireExpandability * 0.85f;
                            }
                        }
                    }

                    if ((num4 & 8) != 0)
                    {
                        value.Time3 -= _lastScanDuration;
                        if (value.Time3 <= 0f)
                        {
                            QueueBurnAway(x - 1, y, z, value.FireExpandability * 0.85f);
                        }

                        foreach (var expansionProbability4 in _expansionProbabilities)
                        {
                            if (_random.Float(0f, 1f) <
                                expansionProbability4.Value *
                                _lastScanDuration *
                                value.FireExpandability)
                            {
                                _toExpand[
                                    new Point3(
                                        x - 1 + expansionProbability4.Key.X,
                                        y + expansionProbability4.Key.Y,
                                        z + expansionProbability4.Key.Z
                                    )] = value.FireExpandability * 0.85f;
                            }
                        }
                    }

                    if (num4 == 0)
                    {
                        value.Time5 -= _lastScanDuration;
                        if (value.Time5 <= 0f)
                        {
                            QueueBurnAway(x, y - 1, z, value.FireExpandability * 0.85f);
                        }
                    }
                }

                _copyIndex++;
            }

            if (_copyIndex >= _firePointsCopy.Count)
            {
                _firePointsCopy.Clear();
            }
        }

        if (_subsystemTime.PeriodicGameTimeEvent(5.0, 0.0))
        {
            foreach (var item in _toBurnAway)
            {
                var key = item.Key;
                var value2 = item.Value;
                SubsystemTerrain.ChangeCell(key.X, key.Y, key.Z, Terrain.ReplaceContents(0, 0));
                if (value2 > 0.25f)
                {
                    for (var i = 0; i < 5; i++)
                    {
                        var point = CellFace.FaceToPoint3(i);
                        SetCellOnFire(key.X + point.X, key.Y + point.Y, key.Z + point.Z, value2);
                    }
                }

                PublishBurnEffect(new Vector3(key) + new Vector3(0.5f), BlockBurnEffectKind.Burning);
            }

            foreach (var item2 in _toExpand)
            {
                SetCellOnFire(item2.Key.X, item2.Key.Y, item2.Key.Z, item2.Value);
            }

            _toBurnAway.Clear();
            _toExpand.Clear();
        }
    }

    public void PublishBurnEffect(Vector3 position, BlockBurnEffectKind kind)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        NetworkSender.SendNearPoint(
            Project,
            position,
            24f,
            new BlockBurnEffectPackage { Position = position, Kind = kind }
        );
    }

    public bool IsCellOnFire(int x, int y, int z)
    {
        for (var i = 0; i < 4; i++)
        {
            var point = CellFace.FaceToPoint3(i);
            var cellValue = SubsystemTerrain.Terrain.GetCellValue(x + point.X, y + point.Y, z + point.Z);
            if (Terrain.ExtractContents(cellValue) == 104)
            {
                var num = Terrain.ExtractData(cellValue);
                var num2 = CellFace.OppositeFace(i);
                if ((num & (1 << num2)) != 0)
                {
                    return true;
                }
            }
        }

        var cellValue2 = SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
        if (Terrain.ExtractContents(cellValue2) == 104 && Terrain.ExtractData(cellValue2) == 0)
        {
            return true;
        }

        return false;
    }

    public bool SetCellOnFire(int x, int y, int z, float fireExpandability, ComponentMiner? miner = null)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return false; }

        var igniting = new CellIgnitingContext(SubsystemTerrain, new Point3(x, y, z), miner);
        WorldRuleHooks.For(Project).Invoke(igniting);
        if (!WorldRuntimeContext.IsProjectActive(igniting.Project)) { return false; }

        if (igniting.Cancel)
        {
            return false;
        }

        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractContents(cellValue);
        if (WorldContentBinding.For(Project).Catalog.Blocks[num].FireDuration == 0f)
        {
            return false;
        }

        var result = false;
        for (var i = 0; i < 5; i++)
        {
            var point = CellFace.FaceToPoint3(i);
            var cellValue2 = SubsystemTerrain.Terrain.GetCellValue(x + point.X, y + point.Y, z + point.Z);
            var num2 = Terrain.ExtractContents(cellValue2);
            if (num2 == 0 || num2 == 104 || num2 == 61)
            {
                var num3 = num2 == 104 ? Terrain.ExtractData(cellValue2) : 0;
                var num4 = CellFace.OppositeFace(i);
                num3 |= (1 << num4) & 0xF;
                cellValue = Terrain.ReplaceData(Terrain.ReplaceContents(0, 104), num3);
                AddFire(x + point.X, y + point.Y, z + point.Z, fireExpandability);
                SubsystemTerrain.ChangeCell(x + point.X, y + point.Y, z + point.Z, cellValue);
                result = true;
            }
        }

        return result;
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        var num = Terrain.ExtractData(SubsystemTerrain.Terrain.GetCellValue(x, y, z));
        if ((num & 1) != 0 &&
            WorldContentBinding.For(Project).Catalog
                .Blocks[SubsystemTerrain.Terrain.GetCellContents(x, y, z + 1)]
                .FireDuration ==
            0f)
        {
            num &= -2;
        }

        if ((num & 2) != 0 &&
            WorldContentBinding.For(Project).Catalog
                .Blocks[SubsystemTerrain.Terrain.GetCellContents(x + 1, y, z)]
                .FireDuration ==
            0f)
        {
            num &= -3;
        }

        if ((num & 4) != 0 &&
            WorldContentBinding.For(Project).Catalog
                .Blocks[SubsystemTerrain.Terrain.GetCellContents(x, y, z - 1)]
                .FireDuration ==
            0f)
        {
            num &= -5;
        }

        if ((num & 8) != 0 &&
            WorldContentBinding.For(Project).Catalog
                .Blocks[SubsystemTerrain.Terrain.GetCellContents(x - 1, y, z)]
                .FireDuration ==
            0f)
        {
            num &= -9;
        }

        if (_fireData.TryGetValue(new Point3(x, y, z), out var value))
        {
            if ((num & 1) != 0 && neighborX == x && neighborY == y && neighborZ == z + 1)
            {
                InitializeFireDataTime(value, 0);
            }

            if ((num & 2) != 0 && neighborX == x + 1 && neighborY == y && neighborZ == z)
            {
                InitializeFireDataTime(value, 1);
            }

            if ((num & 4) != 0 && neighborX == x && neighborY == y && neighborZ == z - 1)
            {
                InitializeFireDataTime(value, 2);
            }

            if ((num & 8) != 0 && neighborX == x - 1 && neighborY == y && neighborZ == z)
            {
                InitializeFireDataTime(value, 3);
            }

            if (num == 0 && neighborX == x && neighborY == y - 1 && neighborZ == z)
            {
                InitializeFireDataTime(value, 5);
            }
        }

        var contents = 104;
        if (num == 0 &&
            WorldContentBinding.For(Project).Catalog
                .Blocks[SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z)].FireDuration ==
            0f)
        {
            contents = 0;
        }

        var value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, contents), num);
        SubsystemTerrain.ChangeCell(x, y, z, value2);
    }

    private bool HasLoadedFuelNeighbors(int x, int z)
    {
        for (var face = 0; face < 4; face++)
        {
            var offset = CellFace.FaceToPoint3(face);
            if (SubsystemTerrain.Terrain.GetChunkAtCell(x + offset.X, z + offset.Z, false) is not
                { HasContents: true })
            {
                return false;
            }
        }

        return true;
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
    {
        AddFire(x, y, z, 1f);
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        RemoveFire(x, y, z);
    }

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
    {
        AddFire(x, y, z, 1f);
    }

    public override void OnChunkDiscarding(TerrainChunk chunk)
    {
        var list = new List<Point3>();
        foreach (var key in _fireData.Keys)
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
            RemoveFire(item.X, item.Y, item.Z);
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        Project.FindSubsystem<SubsystemNetworkInterest>(true)!.ChunkEntered += SendChunkState;
        _subscribedToChunkInterest = true;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        for (var i = -2; i <= 2; i++)
        {
            for (var j = -1; j <= 2; j++)
            {
                for (var k = -2; k <= 2; k++)
                {
                    if (i != 0 || j != 0 || k != 0)
                    {
                        var num = j < 0 ? 1.5f : 2.5f;
                        if (MathUtils.Sqrt(i * i + j * j + k * k) <= num)
                        {
                            var num2 = MathUtils.Sqrt(i * i + k * k);
                            var num3 = j > 0 ? 0.5f * j : -j;
                            _expansionProbabilities[new Point3(i, j, k)] = 0.02f / (num2 + num3);
                        }
                    }
                }
            }
        }
    }

    private void AddFire(int x, int y, int z, float expandability)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            NetworkSender.SendToChunkObservers(
                Project,
                new Point2(x >> 4, z >> 4),
                new BlockFireAddedPackage { X = x, Y = y, Z = z, Duration = expandability }
            );

            AddFireNet(x, y, z, expandability);
        }
    }

    public void AddFireNet(int x, int y, int z, float expandability)
    {
        var point = new Point3(x, y, z);
        if (_fireData.ContainsKey(point))
        {
            return;
        }

        var fireData = new FireData
        {
            Point = point,
            FireExpandability = expandability
        };
        if (WorldRuntimeContext.For(Project).IsAuthority) { InitializeFireDataTimes(fireData); }

        _fireData[point] = fireData;
    }

    public void ClearChunkState(Point2 chunk)
    {
        foreach (var point in _fireData.Keys.Where(point =>
                     point.X >> 4 == chunk.X && point.Z >> 4 == chunk.Y
                 ).ToArray())
        {
            RemoveFireNet(point.X, point.Y, point.Z);
        }
    }

    private void SendChunkState(WorldConnection client, Point2 chunk)
    {
        foreach (var fire in _fireData.Values.Where(fire =>
                     fire.Point.X >> 4 == chunk.X && fire.Point.Z >> 4 == chunk.Y
                 ))
        {
            NetworkSender.SendTo(
                client,
                new BlockFireAddedPackage
                    { X = fire.Point.X, Y = fire.Point.Y, Z = fire.Point.Z, Duration = fire.FireExpandability }
            );
        }
    }

    public override void Dispose()
    {
        if (_subscribedToChunkInterest)
        {
            Project.FindSubsystem<SubsystemNetworkInterest>(true)!.ChunkEntered -= SendChunkState;
            _subscribedToChunkInterest = false;
        }

        base.Dispose();
    }

    private void RemoveFire(int x, int y, int z)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            NetworkSender.SendToChunkObservers(
                Project,
                new Point2(x >> 4, z >> 4),
                new BlockFireRemovedPackage { X = x, Y = y, Z = z }
            );
        }

        RemoveFireNet(x, y, z);
    }

    public void RemoveFireNet(int x, int y, int z)
    {
        var key = new Point3(x, y, z);
        _fireData.Remove(key);
    }

    private void InitializeFireDataTimes(FireData fireData)
    {
        InitializeFireDataTime(fireData, 0);
        InitializeFireDataTime(fireData, 1);
        InitializeFireDataTime(fireData, 2);
        InitializeFireDataTime(fireData, 3);
        InitializeFireDataTime(fireData, 5);
    }

    private void InitializeFireDataTime(FireData fireData, int face)
    {
        var point = CellFace.FaceToPoint3(face);
        var x = fireData.Point.X + point.X;
        var y = fireData.Point.Y + point.Y;
        var z = fireData.Point.Z + point.Z;
        var cellContents = SubsystemTerrain.Terrain.GetCellContents(x, y, z);
        var block = WorldContentBinding.For(Project).Catalog.Blocks[cellContents];
        switch (face)
        {
            case 4:
                break;
            case 0:
                fireData.Time0 = block.FireDuration * _random.Float(0.75f, 1.25f);
                break;
            case 1:
                fireData.Time1 = block.FireDuration * _random.Float(0.75f, 1.25f);
                break;
            case 2:
                fireData.Time2 = block.FireDuration * _random.Float(0.75f, 1.25f);
                break;
            case 3:
                fireData.Time3 = block.FireDuration * _random.Float(0.75f, 1.25f);
                break;
            case 5:
                fireData.Time5 = block.FireDuration * _random.Float(0.75f, 1.25f);
                break;
        }
    }

    private void QueueBurnAway(int x, int y, int z, float expandability)
    {
        var key = new Point3(x, y, z);
        _toBurnAway.TryAdd(key, expandability);
    }

    private class FireData
    {
        public float FireExpandability;

        public Point3 Point;

        public float Time0;

        public float Time1;

        public float Time2;

        public float Time3;

        public float Time5;
    }
}
