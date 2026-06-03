using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemSoilBlockBehavior : SubsystemPollableBlockBehavior, IUpdateable
{
    private readonly Random _random = new();

    private SubsystemTime _subsystemTime = null!;

    private readonly Dictionary<Point3, bool> _toDegrade = new();

    private readonly Dictionary<Point3, bool> _toHydrate = new();

    public override int[] HandledBlocks => [168];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_subsystemTime.PeriodicGameTimeEvent(2.5, 0.0))
        {
            foreach (var key2 in _toDegrade.Keys)
            {
                if (SubsystemTerrain.Terrain.GetCellContents(key2.X, key2.Y, key2.Z) == 168)
                {
                    var cellValue = SubsystemTerrain.Terrain.GetCellValue(key2.X, key2.Y, key2.Z);
                    SubsystemTerrain.ChangeCell(key2.X, key2.Y, key2.Z, Terrain.ReplaceContents(cellValue, 2));
                }
            }

            _toDegrade.Clear();
        }

        if (!_subsystemTime.PeriodicGameTimeEvent(10.0, 0.0))
        {
            return;
        }

        foreach (var (key, value) in _toHydrate)
        {
            var cellValue2 = SubsystemTerrain.Terrain.GetCellValue(key.X, key.Y, key.Z);
            if (Terrain.ExtractContents(cellValue2) != 168)
            {
                continue;
            }

            var data = SoilBlock.SetHydration(Terrain.ExtractData(cellValue2), value);
            var value2 = Terrain.ReplaceData(cellValue2, data);
            SubsystemTerrain.ChangeCell(key.X, key.Y, key.Z, value2);
        }

        _toHydrate.Clear();
    }

    public override void OnCollide(CellFace cellFace, float velocity, ComponentBody componentBody)
    {
        if (componentBody is not { Mass: > 20f, CrouchFactor: 0f })
        {
            return;
        }

        var velocity2 = componentBody.Velocity;
        if (velocity2.Y < -3f || (velocity2.Y < 0f &&
                                  _random.Float(0f, 1f) < 1.5f * _subsystemTime.GameTimeDelta &&
                                  velocity2.LengthSquared() > 1f))
        {
            _toDegrade[cellFace.Point] = true;
        }
    }

    public override void OnPoll(int value, int x, int y, int z, int pollPass)
    {
        var hydration = SoilBlock.GetHydration(Terrain.ExtractData(value));
        if (DetermineHydration(x, y, z, 3))
        {
            if (!hydration)
            {
                _toHydrate[new Point3(x, y, z)] = true;
            }
        }
        else if (hydration)
        {
            _toHydrate[new Point3(x, y, z)] = false;
        }
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
        if (!DegradesSoilIfOnTopOfIt(cellValue))
        {
            return;
        }

        var cellValue2 = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        SubsystemTerrain.ChangeCell(x, y, z, Terrain.ReplaceContents(cellValue2, 2));
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
    }

    private bool DegradesSoilIfOnTopOfIt(int value)
    {
        var num = Terrain.ExtractContents(value);
        var block = BlocksManager.Blocks[num];
        return !block.IsFaceTransparent(SubsystemTerrain, 5, value) && block.Collidable;
    }

    private bool DetermineHydration(int x, int y, int z, int steps)
    {
        if (steps <= 0 || y is <= 0 or >= 255)
        {
            return false;
        }

        if (DetermineHydrationHelper(x - 1, y, z, steps - 1))
        {
            return true;
        }

        if (DetermineHydrationHelper(x + 1, y, z, steps - 1))
        {
            return true;
        }

        if (DetermineHydrationHelper(x, y, z - 1, steps - 1))
        {
            return true;
        }

        if (DetermineHydrationHelper(x, y, z + 1, steps - 1))
        {
            return true;
        }

        if (steps < 2)
        {
            return false;
        }

        return DetermineHydrationHelper(x, y - 1, z, steps - 2) ||
               DetermineHydrationHelper(x, y + 1, z, steps - 2);
    }

    private bool DetermineHydrationHelper(int x, int y, int z, int steps)
    {
        var cellValueFast = SubsystemTerrain.Terrain.GetCellValueFast(x, y, z);
        var num = Terrain.ExtractContents(cellValueFast);
        var data = Terrain.ExtractData(cellValueFast);
        switch (num)
        {
            case 18:
                return true;
            case 168:
                if (SoilBlock.GetHydration(data))
                {
                    return DetermineHydration(x, y, z, steps);
                }

                break;
        }

        return num == 2 && DetermineHydration(x, y, z, steps);
    }
}
