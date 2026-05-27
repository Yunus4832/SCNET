using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemCarpetBlockBehavior : SubsystemPollableBlockBehavior
{
    private readonly Random _random = new();

    private SubsystemWeather _subsystemWeather = null!;

    public override int[] HandledBlocks => [];

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemWeather = Project.FindSubsystem<SubsystemWeather>(true)!;
        base.Load(valuesDictionary);
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var cellContents = SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z);
        if (BlocksManager.Blocks[cellContents].Transparent)
        {
            SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
        }
    }

    public override void OnPoll(int value, int x, int y, int z, int pollPass)
    {
        if (!(_random.Float(0f, 1f) < 0.25f))
        {
            return;
        }

        var precipitationShaftInfo = _subsystemWeather.GetPrecipitationShaftInfo(x, z);
        if (precipitationShaftInfo.Intensity > 0f && y >= precipitationShaftInfo.YLimit - 1)
        {
            SubsystemTerrain.DestroyCell(0, x, y, z, 0, true, false);
        }
    }
}
