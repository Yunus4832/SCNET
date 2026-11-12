using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public abstract class SubsystemFluidBlockBehavior(
    ResourceId fluidId,
    bool generateSources
) : SubsystemBlockBehavior
{
    private FluidBlock _fluidBlock = null!;

    private static readonly Point2[] _sideNeighbors =
    [
        new(-1, 0),
        new(1, 0),
        new(0, -1),
        new(0, 1)
    ];

    private readonly Dictionary<Point3, Vector2> _fluidRandomFlowDirections = new();

    private bool _generateSources = generateSources;

    private readonly Dictionary<Point3, int> _toSet = new();

    private readonly Dictionary<Point3, bool> _toUpdate = new();

    private readonly Dictionary<Point3, int> _visited = new();

    public SubsystemTime SubsystemTime { get; set; } = null!;

    public void UpdateIsTop(int x, int y, int z)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority || y is < 0 or > 255) { return; }

        var terrain = SubsystemTerrain.Terrain;
        var value = terrain.GetCellValue(x, y, z);
        if (!_fluidBlock.IsTheSameFluid(Terrain.ExtractContents(value))) { return; }

        var isTop = y == 255 || !_fluidBlock.IsTheSameFluid(terrain.GetCellContents(x, y + 1, z));
        var updated = Terrain.ReplaceData(value, FluidBlock.SetIsTop(Terrain.ExtractData(value), isTop));
        if (updated != value) { SubsystemTerrain.ChangeCell(x, y, z, updated); }
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
    {
        UpdateIsTop(x, y, z);
    }

    public override void OnBlockModified(int value, int oldValue, int x, int y, int z)
    {
        UpdateIsTop(x, y, z);
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            _toUpdate[new Point3(x, y, z)] = true;
        }

        if (neighborY == y + 1)
        {
            UpdateIsTop(x, y, z);
        }
    }

    public override void OnItemHarvested(
        int x,
        int y,
        int z,
        int blockValue,
        ref BlockDropValue dropValue,
        ref int newBlockValue
    )
    {
        newBlockValue = Terrain.MakeBlockValue(_fluidBlock.BlockIndex);
        dropValue.Value = 0;
        dropValue.Count = 0;
    }

    public float? GetSurfaceHeight(int x, int y, int z, out FluidBlock? surfaceFluidBlock)
    {
        if (y is >= 0 and < 255)
        {
            var chunkAtCell = SubsystemTerrain.Terrain.GetChunkAtCell(x, z, false);
            if (chunkAtCell != null)
            {
                var num = TerrainChunk.CalculateCellIndex(x & 0xF, 0, z & 0xF);
                while (y < 255)
                {
                    var num2 = Terrain.ExtractContents(chunkAtCell.GetCellValueFast(num + y + 1));
                    if (WorldContentBinding.For(Project).Catalog.FluidBlocks[num2] == null)
                    {
                        var cellValueFast = chunkAtCell.GetCellValueFast(num + y);
                        var num3 = Terrain.ExtractContents(cellValueFast);
                        var block = WorldContentBinding.For(Project).Catalog.FluidBlocks[num3];
                        if (block != null)
                        {
                            surfaceFluidBlock = block;
                            var level = FluidBlock.GetLevel(Terrain.ExtractData(cellValueFast));
                            return y + surfaceFluidBlock.GetLevelHeight(level);
                        }

                        surfaceFluidBlock = null;
                        return null;
                    }

                    y++;
                }
            }
        }

        surfaceFluidBlock = null;
        return null;
    }

    public float? GetSurfaceHeight(int x, int y, int z)
    {
        return GetSurfaceHeight(x, y, z, out _);
    }

    public Vector2? CalculateFlowSpeed(int x, int y, int z, out FluidBlock? surfaceBlock, out float? surfaceHeight)
    {
        surfaceHeight = GetSurfaceHeight(x, y, z, out surfaceBlock);
        if (!surfaceHeight.HasValue)
        {
            return null;
        }

        y = (int)surfaceHeight.Value;
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractContents(cellValue);
        if (WorldContentBinding.For(Project).Catalog.Blocks[num] is not FluidBlock)
        {
            return null;
        }

        var cellValue2 = SubsystemTerrain.Terrain.GetCellValue(x - 1, y, z);
        var cellValue3 = SubsystemTerrain.Terrain.GetCellValue(x + 1, y, z);
        var cellValue4 = SubsystemTerrain.Terrain.GetCellValue(x, y, z - 1);
        var cellValue5 = SubsystemTerrain.Terrain.GetCellValue(x, y, z + 1);
        var num2 = Terrain.ExtractContents(cellValue2);
        var num3 = Terrain.ExtractContents(cellValue3);
        var num4 = Terrain.ExtractContents(cellValue4);
        var num5 = Terrain.ExtractContents(cellValue5);
        var level = FluidBlock.GetLevel(Terrain.ExtractData(cellValue));
        var num6 = num2 == num ? FluidBlock.GetLevel(Terrain.ExtractData(cellValue2)) : level;
        var num7 = num3 == num ? FluidBlock.GetLevel(Terrain.ExtractData(cellValue3)) : level;
        var num8 = num4 == num ? FluidBlock.GetLevel(Terrain.ExtractData(cellValue4)) : level;
        var num9 = num5 == num ? FluidBlock.GetLevel(Terrain.ExtractData(cellValue5)) : level;
        Vector2 vector = default;
        vector.X = MathUtils.Sign(level - num6) - MathUtils.Sign(level - num7);
        vector.Y = MathUtils.Sign(level - num8) - MathUtils.Sign(level - num9);
        var v = vector;
        if (v.LengthSquared() > 1f)
        {
            v = Vector2.Normalize(v);
        }

        if (!_fluidRandomFlowDirections.TryGetValue(new Point3(x, y, z), out var value))
        {
            value.X = 0.05f *
                      (2f *
                       SimplexNoise.OctavedNoise(
                           x + 0.2f * (float)SubsystemTime.GameTime,
                           z,
                           0.1f,
                           1,
                           1f,
                           1f
                       ) -
                       1f);
            value.Y = 0.05f *
                      (2f *
                       SimplexNoise.OctavedNoise(
                           x + 0.2f * (float)SubsystemTime.GameTime + 100f,
                           z,
                           0.1f,
                           1,
                           1f,
                           1f
                       ) -
                       1f);
            if (_fluidRandomFlowDirections.Count < 1000)
            {
                _fluidRandomFlowDirections[new Point3(x, y, z)] = value;
            }
            else
            {
                _fluidRandomFlowDirections.Clear();
            }
        }

        v += value;
        return v * 2f;
    }

    public Vector2? CalculateFlowSpeed(int x, int y, int z)
    {
        return CalculateFlowSpeed(x, y, z, out _, out _);
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _fluidBlock = (FluidBlock)SubsystemTerrain.Catalog.GetBlock(fluidId);
        SubsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
    }

    public void SpreadFluid()
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        for (var i = 0; i < 2; i++)
        {
            foreach (var key in _toUpdate.Keys)
            {
                var x = key.X;
                var y = key.Y;
                var z = key.Z;
                var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
                var contents = Terrain.ExtractContents(cellValue);
                var data = Terrain.ExtractData(cellValue);
                var level = FluidBlock.GetLevel(data);
                if (_fluidBlock.IsTheSameFluid(contents))
                {
                    var cellValue2 = SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
                    var contents2 = Terrain.ExtractContents(cellValue2);
                    var data2 = Terrain.ExtractData(cellValue2);
                    var level2 = FluidBlock.GetLevel(data2);
                    var num = _fluidBlock.MaxLevel + 1;
                    var num2 = 0;
                    for (var j = 0; j < 4; j++)
                    {
                        var cellValue3 =
                            SubsystemTerrain.Terrain.GetCellValue(
                                x + _sideNeighbors[j].X,
                                y,
                                z + _sideNeighbors[j].Y
                            );
                        var contents3 = Terrain.ExtractContents(cellValue3);
                        if (_fluidBlock.IsTheSameFluid(contents3))
                        {
                            var level3 = FluidBlock.GetLevel(Terrain.ExtractData(cellValue3));
                            num = MathUtils.Min(num, level3);
                            if (level3 == 0)
                            {
                                num2++;
                            }
                        }
                    }

                    if (level != 0 && level <= num)
                    {
                        var contents4 = Terrain.ExtractContents(SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z));
                        if (!_fluidBlock.IsTheSameFluid(contents4))
                        {
                            if (num + 1 > _fluidBlock.MaxLevel)
                            {
                                Set(x, y, z, 0);
                            }
                            else
                            {
                                Set(x, y, z, Terrain.MakeBlockValue(contents, FluidBlock.SetLevel(data, num + 1)));
                            }

                            continue;
                        }
                    }

                    if (_generateSources && level != 0 && num2 >= 2)
                    {
                        Set(x, y, z, Terrain.MakeBlockValue(contents, FluidBlock.SetLevel(data, 0)));
                    }
                    else if (_fluidBlock.IsTheSameFluid(contents2))
                    {
                        if (level2 > 1)
                        {
                            Set(x, y - 1, z, Terrain.MakeBlockValue(contents2, FluidBlock.SetLevel(data2, 1)));
                        }
                    }
                    else if (!OnFluidInteract(
                                 cellValue2,
                                 x,
                                 y - 1,
                                 z,
                                 Terrain.MakeBlockValue(_fluidBlock.BlockIndex, FluidBlock.SetLevel(0, 1))
                             ) &&
                             level < _fluidBlock.MaxLevel)
                    {
                        _visited.Clear();
                        var num3 = LevelAtNearestFall(x + 1, y, z, level + 1, _visited);
                        var num4 = LevelAtNearestFall(x - 1, y, z, level + 1, _visited);
                        var num5 = LevelAtNearestFall(x, y, z + 1, level + 1, _visited);
                        var num6 = LevelAtNearestFall(x, y, z - 1, level + 1, _visited);
                        var num7 = MathUtils.Min(num3, num4, num5, num6);
                        if (num3 == num7)
                        {
                            FlowTo(x + 1, y, z, level + 1);
                            FlowTo(x, y, z - 1, _fluidBlock.MaxLevel);
                            FlowTo(x, y, z + 1, _fluidBlock.MaxLevel);
                        }

                        if (num4 == num7)
                        {
                            FlowTo(x - 1, y, z, level + 1);
                            FlowTo(x, y, z - 1, _fluidBlock.MaxLevel);
                            FlowTo(x, y, z + 1, _fluidBlock.MaxLevel);
                        }

                        if (num5 == num7)
                        {
                            FlowTo(x, y, z + 1, level + 1);
                            FlowTo(x - 1, y, z, _fluidBlock.MaxLevel);
                            FlowTo(x + 1, y, z, _fluidBlock.MaxLevel);
                        }

                        if (num6 == num7)
                        {
                            FlowTo(x, y, z - 1, level + 1);
                            FlowTo(x - 1, y, z, _fluidBlock.MaxLevel);
                            FlowTo(x + 1, y, z, _fluidBlock.MaxLevel);
                        }
                    }
                }
            }

            _toUpdate.Clear();
            foreach (var item in _toSet)
            {
                var x2 = item.Key.X;
                var y2 = item.Key.Y;
                var z2 = item.Key.Z;
                var value = item.Value;
                var contents5 = Terrain.ExtractContents(item.Value);
                var cellContents = SubsystemTerrain.Terrain.GetCellContents(x2, y2, z2);
                var block = WorldContentBinding.For(Project).Catalog.FluidBlocks[cellContents];
                if (block != null && !block.IsTheSameFluid(contents5))
                {
                    SubsystemTerrain.DestroyCell(0, x2, y2, z2, value, false, false);
                }
                else
                {
                    SubsystemTerrain.ChangeCell(x2, y2, z2, value);
                }
            }

            _toSet.Clear();
            SubsystemTerrain.ProcessModifiedCells();
        }
    }

    public virtual bool OnFluidInteract(int interactValue, int x, int y, int z, int fluidValue)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return false; }

        if (WorldContentBinding.For(Project).Catalog.Blocks[Terrain.ExtractContents(interactValue)]
            .FluidBlocker)
        {
            return false;
        }

        SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
        Set(x, y, z, fluidValue);
        return true;
    }

    public float? CalculateDistanceToFluid(Vector3 p, int radius, bool flowingFluidOnly)
    {
        var num = float.MaxValue;
        var terrain = SubsystemTerrain.Terrain;
        var num2 = Terrain.ToCell(p.X) - radius;
        var num3 = Terrain.ToCell(p.X) + radius;
        var num4 = MathUtils.Clamp(Terrain.ToCell(p.Y) - radius, 0, 254);
        var num5 = MathUtils.Clamp(Terrain.ToCell(p.Y) + radius, 0, 254);
        var num6 = Terrain.ToCell(p.Z) - radius;
        var num7 = Terrain.ToCell(p.Z) + radius;
        for (var i = num6; i <= num7; i++)
        {
            for (var j = num2; j <= num3; j++)
            {
                var chunkAtCell = terrain.GetChunkAtCell(j, i, false);
                if (chunkAtCell == null)
                {
                    continue;
                }

                var k = TerrainChunk.CalculateCellIndex(j & 0xF, num4, i & 0xF);
                for (var l = num4; l <= num5; l++, k++)
                {
                    var cellValueFast = chunkAtCell.GetCellValueFast(k);
                    var contents = Terrain.ExtractContents(cellValueFast);
                    if (!_fluidBlock.IsTheSameFluid(contents))
                    {
                        continue;
                    }

                    if (flowingFluidOnly)
                    {
                        if (FluidBlock.GetLevel(Terrain.ExtractData(cellValueFast)) == 0)
                        {
                            continue;
                        }

                        var contents2 = Terrain.ExtractContents(chunkAtCell.GetCellValueFast(k + 1));
                        if (_fluidBlock.IsTheSameFluid(contents2))
                        {
                            continue;
                        }
                    }

                    var num8 = p.X - (j + 0.5f);
                    var num9 = p.Y - (l + 1f);
                    var num10 = p.Z - (i + 0.5f);
                    var num11 = num8 * num8 + num9 * num9 + num10 * num10;
                    if (num11 < num)
                    {
                        num = num11;
                    }
                }
            }
        }

        if (num.CloseTo(float.MaxValue))
        {
            return null;
        }

        return MathUtils.Sqrt(num);
    }

    public void Set(int x, int y, int z, int value)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        var key = new Point3(x, y, z);
        _toSet.TryAdd(key, value);
    }

    public void FlowTo(int x, int y, int z, int level)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        if (level > _fluidBlock.MaxLevel)
        {
            return;
        }

        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        var contents = Terrain.ExtractContents(cellValue);
        var data = Terrain.ExtractData(cellValue);
        if (_fluidBlock.IsTheSameFluid(contents))
        {
            var level2 = FluidBlock.GetLevel(data);
            if (level < level2)
            {
                Set(x, y, z, Terrain.MakeBlockValue(contents, FluidBlock.SetLevel(data, level)));
            }
        }
        else
        {
            OnFluidInteract(
                cellValue,
                x,
                y,
                z,
                Terrain.MakeBlockValue(_fluidBlock.BlockIndex, FluidBlock.SetLevel(0, level))
            );
        }
    }

    public int LevelAtNearestFall(int x, int y, int z, int level, Dictionary<Point3, int> levels)
    {
        if (level > _fluidBlock.MaxLevel)
        {
            return int.MaxValue;
        }

        if (!levels.TryGetValue(new Point3(x, y, z), out var value))
        {
            value = int.MaxValue;
        }

        if (level >= value)
        {
            return int.MaxValue;
        }

        levels[new Point3(x, y, z)] = level;
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractContents(cellValue);
        if (_fluidBlock.IsTheSameFluid(num))
        {
            if (FluidBlock.GetLevel(Terrain.ExtractData(cellValue)) < level)
            {
                return int.MaxValue;
            }
        }
        else if (WorldContentBinding.For(Project).Catalog.Blocks[num].FluidBlocker)
        {
            return int.MaxValue;
        }

        var num2 = Terrain.ExtractContents(SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z));
        var block = WorldContentBinding.For(Project).Catalog.Blocks[num2];
        if (_fluidBlock.IsTheSameFluid(num2) || !block.FluidBlocker)
        {
            return level;
        }

        var x2 = LevelAtNearestFall(x - 1, y, z, level + 1, levels);
        var x3 = LevelAtNearestFall(x + 1, y, z, level + 1, levels);
        var x4 = LevelAtNearestFall(x, y, z - 1, level + 1, levels);
        var x5 = LevelAtNearestFall(x, y, z + 1, level + 1, levels);
        return MathUtils.Min(x2, x3, x4, x5);
    }
}
