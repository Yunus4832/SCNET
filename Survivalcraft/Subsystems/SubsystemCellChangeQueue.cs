using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemCellChangeQueue : Subsystem, IUpdateable
{
    private readonly Dictionary<Point3, CellChange> _toChange = new();

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    UpdateOrder IUpdateable.UpdateOrder => UpdateOrder.Default;

    void IUpdateable.Update(float dt)
    {
        if (_subsystemTime.PeriodicGameTimeEvent(20.0, 0.0))
        {
            ApplyCellChanges();
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
    }

    public void QueueCellChange(int x, int y, int z, int value, bool applyImmediately = false)
    {
        if (applyImmediately)
        {
            _subsystemTerrain.ChangeCell(x, y, z, value);
        }
        else
        {
            _toChange[new Point3(x, y, z)] = new CellChange
            {
                Value = value,
                RequiredValue = _subsystemTerrain.Terrain.GetCellValue(x, y, z)
            };
        }

        if (_toChange.Count >= 10000)
        {
            ApplyCellChanges();
        }
    }

    private void ApplyCellChanges()
    {
        foreach (var (key, value) in _toChange)
        {
            if (Terrain.ReplaceLight(_subsystemTerrain.Terrain.GetCellValue(key.X, key.Y, key.Z), 0) ==
                Terrain.ReplaceLight(value.RequiredValue, 0))
            {
                _subsystemTerrain.ChangeCell(key.X, key.Y, key.Z, value.Value);
            }
        }

        _toChange.Clear();
    }

    private struct CellChange
    {
        public int RequiredValue;

        public int Value;
    }
}
