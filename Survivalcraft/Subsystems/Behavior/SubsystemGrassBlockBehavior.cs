using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Subsystems;

public class SubsystemGrassBlockBehavior : SubsystemPollableBlockBehavior, IUpdateable
{
    private readonly Random _random = new();

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemTime _subsystemTime = null!;

    private readonly Dictionary<Point3, int> _toUpdate = new();

    public override int[] HandledBlocks => [8];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        if (!_subsystemTime.PeriodicGameTimeEvent(60.0, 0.0))
        {
            return;
        }

        if (CommonLib.WorkType == WorkType.Server)
        {
            NetUpdate();
        }
    }

    public override void OnPoll(int value, int x, int y, int z, int pollPass)
    {
        if (Terrain.ExtractData(value) != 0 || _subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode != 0)
        {
            return;
        }

        var num = Terrain.ExtractLight(SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z));
        if (num == 0)
        {
            _toUpdate[new Point3(x, y, z)] = Terrain.ReplaceContents(value, 8);
        }

        if (num < 13)
        {
            return;
        }

        for (var i = x - 1; i <= x + 1; i++)
        {
            for (var j = z - 1; j <= z + 1; j++)
            {
                for (var k = y - 2; k <= y + 1; k++)
                {
                    var cellValue = SubsystemTerrain.Terrain.GetCellValue(i, k, j);
                    if (Terrain.ExtractContents(cellValue) != 2)
                    {
                        continue;
                    }

                    var cellValue2 = SubsystemTerrain.Terrain.GetCellValue(i, k + 1, j);
                    if (KillsGrassIfOnTopOfIt(cellValue2) || Terrain.ExtractLight(cellValue2) < 13 ||
                        !(_random.Float(0f, 1f) < 0.1f))
                    {
                        continue;
                    }

                    var num2 = Terrain.ReplaceContents(cellValue, 8);
                    _toUpdate[new Point3(i, k, j)] = num2;
                    if (Terrain.ExtractContents(cellValue2) == 0)
                    {
                        var temperature = SubsystemTerrain.Terrain.GetTemperature(i, j);
                        var humidity = SubsystemTerrain.Terrain.GetHumidity(i, j);
                        var num3 = PlantsManager.GenerateRandomPlantValue(_random, num2, temperature, humidity, k + 1);
                        if (num3 != 0)
                        {
                            _toUpdate[new Point3(i, k + 1, j)] = num3;
                        }
                    }
                }
            }
        }
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
        if (Terrain.ExtractContents(cellValue) == 61)
        {
            var cellValueFast = SubsystemTerrain.Terrain.GetCellValueFast(x, y, z);
            cellValueFast = Terrain.ReplaceData(cellValueFast, 1);
            SubsystemTerrain.ChangeCell(x, y, z, cellValueFast);
        }
        else
        {
            var cellValueFast2 = SubsystemTerrain.Terrain.GetCellValueFast(x, y, z);
            cellValueFast2 = Terrain.ReplaceData(cellValueFast2, 0);
            SubsystemTerrain.ChangeCell(x, y, z, cellValueFast2);
        }

        if (KillsGrassIfOnTopOfIt(cellValue))
        {
            SubsystemTerrain.ChangeCell(x, y, z, Terrain.MakeBlockValue(2, 0, 0));
        }
    }

    public override void OnExplosion(int value, int x, int y, int z, float damage)
    {
        if (damage > BlocksManager.Blocks[8].ExplosionResilience * SubsystemExplosions.SharedRandom.Float(0f, 1f))
        {
            SubsystemTerrain.ChangeCell(x, y, z, Terrain.MakeBlockValue(2, 0, 0));
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        base.Load(valuesDictionary);
    }

    private void NetUpdate()
    {
        foreach (var item in _toUpdate)
        {
            if (Terrain.ExtractContents(item.Value) == 8)
            {
                if (SubsystemTerrain.Terrain.GetCellContents(item.Key.X, item.Key.Y, item.Key.Z) != 2)
                {
                    continue;
                }
            }
            else
            {
                var cellContents = SubsystemTerrain.Terrain.GetCellContents(item.Key.X, item.Key.Y - 1, item.Key.Z);
                if ((cellContents != 8 && cellContents != 2) ||
                    SubsystemTerrain.Terrain.GetCellContents(item.Key.X, item.Key.Y, item.Key.Z) != 0)
                {
                    continue;
                }
            }

            SubsystemTerrain.ChangeCell(item.Key.X, item.Key.Y, item.Key.Z, item.Value);
        }

        _toUpdate.Clear();
    }

    private bool KillsGrassIfOnTopOfIt(int value)
    {
        var num = Terrain.ExtractContents(value);
        var block = BlocksManager.Blocks[num];
        if (block is not FluidBlock)
        {
            return !block.IsFaceTransparent(SubsystemTerrain, 5, value) && block.Collidable;
        }

        return true;
    }
}
