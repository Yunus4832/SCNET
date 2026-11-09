using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemDeciduousLeavesBlockBehavior : SubsystemPollableBlockBehavior
{
    private SubsystemCellChangeQueue _subsystemCellChangeQueue = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    /// <summary>A leaf cell encountered by normal generation or polling; consumers own any presentation scheduling.</summary>
    public event Action<int, Point3>? LeafCellVisited;

    public override ResourceId[] HandledBlocks => [];

    public void CreateFallenLeaves(Point3 p, bool applyImmediately)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority) { return; }

        if (FindFallenLeavesPosition(p) is { } target)
        {
            if (!_subsystemCellChangeQueue.TryQueueCellChange(
                    target.X,
                    target.Y,
                    target.Z,
                    Terrain.MakeBlockValue(261),
                    applyImmediately
                )) { return; }
        }
    }

    private Point3? FindFallenLeavesPosition(Point3 p)
    {
        int? num = null;
        while (p.Y is >= 1 and < 256)
        {
            var cellValue = _subsystemTerrain.Terrain.GetCellValue(p.X, p.Y, p.Z);
            if (num.HasValue)
            {
                if (SubsystemFallenLeavesBlockBehavior.CanSupportFallenLeaves(SubsystemTerrain.Catalog, cellValue) &&
                    SubsystemFallenLeavesBlockBehavior.CanBeReplacedByFallenLeaves(num.Value))
                {
                    return new Point3(p.X, p.Y + 1, p.Z);
                }

                if (SubsystemFallenLeavesBlockBehavior.StopsFallenLeaves(SubsystemTerrain.Catalog, cellValue))
                {
                    break;
                }
            }

            num = cellValue;
            p.Y--;
        }

        return null;
    }

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
    {
        UpdateTimeOfYear(value, x, y, z, true);
        if (WorldRuntimeContext.IsProjectActive(Project)) { LeafCellVisited?.Invoke(value, new Point3(x, y, z)); }
    }

    public override void OnPoll(int value, int x, int y, int z, int pollPass)
    {
        UpdateTimeOfYear(value, x, y, z, false);
        if (WorldRuntimeContext.IsProjectActive(Project)) { LeafCellVisited?.Invoke(value, new Point3(x, y, z)); }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemCellChangeQueue = Project.FindSubsystem<SubsystemCellChangeQueue>(true)!;
    }

    public void UpdateTimeOfYear(int value, int x, int y, int z, bool applyImmediately)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority) { return; }

        var num = 0.03f * MathUtils.Hash((uint)(x + y * 59 + z * 3319)) / 4.2949673E+09f;
        var timeOfYear = IntervalUtils.Normalize(_subsystemGameInfo.WorldSettings.TimeOfYear + num);
        var obj = (DeciduousLeavesBlock)WorldContentBinding.For(Project).Catalog
            .Blocks[Terrain.ExtractContents(value)];
        var num2 = Terrain.ExtractData(value);
        var num3 = obj.SetTimeOfYear(num2, timeOfYear);
        if (num3 == num2)
        {
            return;
        }

        var value2 = Terrain.ReplaceData(value, num3);
        var season = DeciduousLeavesBlock.GetSeason(num2);
        var enteringWinter = DeciduousLeavesBlock.GetSeason(num3) == Season.Winter && season != Season.Winter;
        if (!applyImmediately && enteringWinter && FindFallenLeavesPosition(new Point3(x, y, z)) is { } target)
        {
            if (!_subsystemCellChangeQueue.TryQueueCellChanges(
                    [(new Point3(x, y, z), value2), (target, Terrain.MakeBlockValue(261))]
                )) { return; }
        }
        else
        {
            if (!_subsystemCellChangeQueue.TryQueueCellChange(x, y, z, value2, applyImmediately)) { return; }

            if (enteringWinter) { CreateFallenLeaves(new Point3(x, y, z), applyImmediately); }
        }
    }

    public override void Dispose()
    {
        LeafCellVisited = null;
        base.Dispose();
    }
}
