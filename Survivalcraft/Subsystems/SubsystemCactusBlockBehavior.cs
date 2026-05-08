using EntitySystem.TemplatesDatabase;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Subsystems;

public class SubsystemCactusBlockBehavior : SubsystemPollableBlockBehavior, IUpdateable
{
    private readonly Random _random = new();

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemTime _subsystemTime = null!;

    private readonly Dictionary<Point3, int> _toUpdate = new();

    public override int[] HandledBlocks => [127];

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
        var cellContents = SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z);
        if (cellContents != 7 && cellContents != 127)
        {
            SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
        }
    }

    public override void OnPoll(int value, int x, int y, int z, int pollPass)
    {
        if (_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode != 0)
        {
            return;
        }

        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
        if (Terrain.ExtractContents(cellValue) != 0 || Terrain.ExtractLight(cellValue) < 12)
        {
            return;
        }

        var cellContents = SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z);
        var cellContents2 = SubsystemTerrain.Terrain.GetCellContents(x, y - 2, z);
        if ((cellContents != 127 || cellContents2 != 127) && _random.Float(0f, 1f) < 0.25f)
        {
            _toUpdate[new Point3(x, y + 1, z)] = Terrain.MakeBlockValue(127, 0, 0);
        }
    }

    public override void OnCollide(CellFace cellFace, float velocity, ComponentBody componentBody)
    {
        var creature = componentBody.Entity.FindComponent<ComponentCreature>();
        if (creature == null)
        {
            return;
        }

        const string cuase = "被仙人掌刺入";
        var amount = 0.01f * MathUtils.Abs(velocity);
        if (CommonLib.WorkType != WorkType.Client)
        {
            creature.ComponentHealth.Injure(amount, null, false, cuase);
        }
        else
        {
            CommonLib.Net.QueuePackage(new ComponentHealthPackage(creature.ComponentHealth, null, amount, cuase,
                false, true, ComponentHealthPackage.RequestInjureType.Cactus));
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        base.Load(valuesDictionary);
    }
}
