using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemIvyBlockBehavior : SubsystemPollableBlockBehavior, IUpdateable
{
    private readonly Random _random = new();

    private SubsystemTime _subsystemTime = null!;

    private readonly Dictionary<Point3, int> _toUpdate = new();

    public override int[] HandledBlocks => [];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!_subsystemTime.PeriodicGameTimeEvent(60.0, 0.0))
        {
            return;
        }

        foreach (var item in _toUpdate)
        {
            if (SubsystemTerrain.Terrain.GetCellContents(item.Key.X, item.Key.Y, item.Key.Z) == 0)
            {
                SubsystemTerrain.ChangeCell(item.Key.X, item.Key.Y, item.Key.Z, item.Value);
            }
        }

        _toUpdate.Clear();
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var face = IvyBlock.GetFace(Terrain.ExtractData(SubsystemTerrain.Terrain.GetCellValue(x, y, z)));
        var flag = false;
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
        if (Terrain.ExtractContents(cellValue) == 197 &&
            IvyBlock.GetFace(Terrain.ExtractData(cellValue)) == face)
        {
            flag = true;
        }

        if (flag)
        {
            return;
        }

        var point = CellFace.FaceToPoint3(face);
        var cellValue2 = SubsystemTerrain.Terrain.GetCellValue(x + point.X, y + point.Y, z + point.Z);
        if (!BlocksManager.Blocks[Terrain.ExtractContents(cellValue2)].Collidable)
        {
            SubsystemTerrain.DestroyCell(0, x, y, z, 0, true, false);
        }
    }

    public override void OnPoll(int value, int x, int y, int z, int pollPass)
    {
        if (_random.Float(0f, 1f) < 0.5f && !IvyBlock.IsGrowthStopCell(x, y, z) &&
            Terrain.ExtractContents(SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z)) == 0)
        {
            _toUpdate[new Point3(x, y - 1, z)] = value;
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
    }
}
