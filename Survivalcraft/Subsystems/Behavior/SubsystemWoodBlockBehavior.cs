using Engine.Serialization;

using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemWoodBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    public const int Radius = 3;

    public const int MaxLeavesToCheck = 5000;

    private readonly HashSet<Point3> _leavesToCheck = [];

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemTime _subsystemTime = null!;

    public override int[] HandledBlocks => [];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_leavesToCheck.Count <= 0 || !_subsystemTime.PeriodicGameTimeEvent(20.0, 0.0))
        {
            return;
        }

        var num = MathUtils.Min(MathUtils.Max((int)(_leavesToCheck.Count * 0.1f), 10), 200);
        for (var i = 0; i < num; i++)
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
        if (_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode != 0 || _leavesToCheck.Count >= 5000 ||
            BlocksManager.Blocks[Terrain.ExtractContents(value)] is not WoodBlock)
        {
            return;
        }

        var num = x - 3;
        var num2 = MathUtils.Max(y - 3, 0);
        var num3 = z - 3;
        var num4 = x + 3;
        var num5 = MathUtils.Min(y + 3, 255);
        var num6 = z + 3;
        for (var i = num; i <= num4; i++)
        for (var j = num3; j <= num6; j++)
        {
            var chunkAtCell = SubsystemTerrain.Terrain.GetChunkAtCell(i, j, false);
            if (chunkAtCell == null)
            {
                continue;
            }

            var num7 = TerrainChunk.CalculateCellIndex(i & 0xF, 0, j & 0xF);
            for (var k = num2; k <= num5; k++)
            {
                var num8 = Terrain.ExtractContents(chunkAtCell.GetCellValueFast(num7 + k));
                if (num8 != 0 && BlocksManager.Blocks[num8] is LeavesBlock)
                {
                    _leavesToCheck.Add(new Point3(i, k, j));
                }
            }
        }
    }

    public override void OnChunkDiscarding(TerrainChunk chunk)
    {
        var num = chunk.Origin.X - 16;
        var num2 = chunk.Origin.Y - 16;
        var num3 = chunk.Origin.X + 32;
        var num4 = chunk.Origin.Y + 32;
        var list = new List<Point3>();
        foreach (var item in _leavesToCheck)
        {
            if (item.X >= num && item.X < num3 && item.Z >= num2 && item.Z < num4)
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
        if (!(BlocksManager.Blocks[SubsystemTerrain.Terrain.GetCellContents(p.X, p.Y, p.Z)] is LeavesBlock))
        {
            return;
        }

        var flag = false;
        var num = p.X - 3;
        var num2 = MathUtils.Max(p.Y - 3, 0);
        var num3 = p.Z - 3;
        var num4 = p.X + 3;
        var num5 = MathUtils.Min(p.Y + 3, 255);
        var num6 = p.Z + 3;
        for (var i = num; i <= num4; i++)
        {
            for (var j = num3; j <= num6; j++)
            {
                var chunkAtCell = SubsystemTerrain.Terrain.GetChunkAtCell(i, j, false);
                if (chunkAtCell == null)
                {
                    continue;
                }

                var num7 = TerrainChunk.CalculateCellIndex(i & 0xF, 0, j & 0xF);
                var num8 = num2;
                while (num8 <= num5)
                {
                    var num9 = Terrain.ExtractContents(chunkAtCell.GetCellValueFast(num7 + num8));
                    if (num9 == 0 || BlocksManager.Blocks[num9] is not WoodBlock)
                    {
                        num8++;
                        continue;
                    }

                    goto IL_00e8;
                }
            }

            continue;
            IL_00e8:
            flag = true;
            break;
        }

        if (!flag)
        {
            SubsystemTerrain.ChangeCell(p.X, p.Y, p.Z, 0);
        }
    }
}
