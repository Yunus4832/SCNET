using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemFallenLeavesBlockBehavior : SubsystemPollableBlockBehavior
{
    private readonly Random _random = new();

    private SubsystemSeasons _subsystemSeasons = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    public override int[] HandledBlocks => [];

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        if (!CanSupportFallenLeaves(SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z)))
        {
            SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
        }
    }

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
    {
        UpdateFallenLeaves(x, y, z);
    }

    public override void OnPoll(int value, int x, int y, int z, int pollPass)
    {
        if (_random.Bool(0.5f))
        {
            UpdateFallenLeaves(x, y, z);
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemSeasons = Project.FindSubsystem<SubsystemSeasons>(true)!;
    }

    public static bool CanSupportFallenLeaves(int value)
    {
        var num = Terrain.ExtractContents(value);
        return !BlocksManager.Blocks[num].Transparent;
    }

    public static bool StopsFallenLeaves(int value)
    {
        var num = Terrain.ExtractContents(value);
        var block = BlocksManager.Blocks[num];
        if (block is not AirBlock)
        {
            return block is not LeavesBlock;
        }

        return false;
    }

    public static bool CanBeReplacedByFallenLeaves(int value)
    {
        return Terrain.ExtractContents(value) == 0;
    }

    private void UpdateFallenLeaves(int x, int y, int z)
    {
        if (_subsystemSeasons.Season is Season.Spring or Season.Summer)
        {
            _subsystemTerrain.DestroyCell(0, x, y, z, Terrain.MakeBlockValue(0), true, true);
        }
    }
}
