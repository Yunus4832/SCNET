using Engine.Core.Serialization;

using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemWoodBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    public const int Radius = 3;

    public const int MaxLeavesToCheck = 5000;

    private readonly HashSet<Point3> _leavesToCheck = [];

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemTime _subsystemTime = null!;

    public override ResourceId[] HandledBlocks => [];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_leavesToCheck.Count <= 0 || !_subsystemTime.PeriodicGameTimeEvent(20.0, 0.0))
        {
            return;
        }

        var checkBudget = MathUtils.Min(MathUtils.Max((int)(_leavesToCheck.Count * 0.1f), 10), 200);
        for (var i = 0; i < checkBudget; i++)
        {
            if (_leavesToCheck.Count <= 0)
            {
                break;
            }

            DecayLeavesIfNeeded(_leavesToCheck.First());
        }
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        if (_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode != 0 ||
            _leavesToCheck.Count >= 5000 ||
            WorldContentBinding.For(Project).Catalog
                .Blocks[Terrain.ExtractContents(value)] is not WoodBlock)
        {
            return;
        }

        var minX = x - 3;
        var minY = MathUtils.Max(y - 3, 0);
        var minZ = z - 3;
        var maxX = x + 3;
        var maxY = MathUtils.Min(y + 3, 255);
        var maxZ = z + 3;
        for (var i = minX; i <= maxX; i++)
        {
            for (var j = minZ; j <= maxZ; j++)
            {
                var chunkAtCell = SubsystemTerrain.Terrain.GetChunkAtCell(i, j, false);
                if (chunkAtCell == null)
                {
                    continue;
                }

                var columnIndex = TerrainChunk.CalculateCellIndex(i & 0xF, 0, j & 0xF);
                for (var k = minY; k <= maxY; k++)
                {
                    var contents = Terrain.ExtractContents(chunkAtCell.GetCellValueFast(columnIndex + k));
                    if (contents != 0 &&
                        WorldContentBinding.For(Project).Catalog.Blocks[contents] is LeavesBlock)
                    {
                        _leavesToCheck.Add(new Point3(i, k, j));
                    }
                }
            }
        }
    }

    public override void OnChunkDiscarding(TerrainChunk chunk)
    {
        var minX = chunk.Origin.X - 16;
        var minZ = chunk.Origin.Y - 16;
        var endX = chunk.Origin.X + 32;
        var endZ = chunk.Origin.Y + 32;
        var list = new List<Point3>();
        foreach (var item in _leavesToCheck)
        {
            if (item.X >= minX && item.X < endX && item.Z >= minZ && item.Z < endZ)
            {
                list.Add(item);
            }
        }

        foreach (var item2 in list)
        {
            DecayLeavesIfNeeded(item2);
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        var value = valuesDictionary.GetValue<string>("LeavesToCheck");
        var array = HumanReadableConverter.ValuesListFromString<Point3>(';', value);
        foreach (var item in array)
        {
            _leavesToCheck.Add(item);
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        base.Save(valuesDictionary);
        var value = HumanReadableConverter.ValuesListToString(';', _leavesToCheck.ToArray());
        valuesDictionary.SetValue("LeavesToCheck", value);
    }

    private void DecayLeavesIfNeeded(Point3 p)
    {
        _leavesToCheck.Remove(p);
        if (!(WorldContentBinding.For(Project).Catalog
                .Blocks[SubsystemTerrain.Terrain.GetCellContents(p.X, p.Y, p.Z)] is LeavesBlock))
        {
            return;
        }

        var foundWood = false;
        var minX = p.X - 3;
        var minY = MathUtils.Max(p.Y - 3, 0);
        var minZ = p.Z - 3;
        var maxX = p.X + 3;
        var maxY = MathUtils.Min(p.Y + 3, 255);
        var maxZ = p.Z + 3;
        for (var i = minX; i <= maxX; i++)
        {
            for (var j = minZ; j <= maxZ; j++)
            {
                var chunkAtCell = SubsystemTerrain.Terrain.GetChunkAtCell(i, j, false);
                if (chunkAtCell == null)
                {
                    continue;
                }

                var columnIndex = TerrainChunk.CalculateCellIndex(i & 0xF, 0, j & 0xF);
                var cellY = minY;
                while (cellY <= maxY)
                {
                    var contents = Terrain.ExtractContents(chunkAtCell.GetCellValueFast(columnIndex + cellY));
                    if (contents == 0 ||
                        WorldContentBinding.For(Project).Catalog.Blocks[contents] is not WoodBlock)
                    {
                        cellY++;
                        continue;
                    }

                    goto IL_00e8;
                }
            }

            continue;
            IL_00e8:
            foundWood = true;
            break;
        }

        if (!foundWood)
        {
            SubsystemTerrain.ChangeCell(p.X, p.Y, p.Z, 0);
        }
    }
}
