using Engine.Serialization;

using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemMagnetBlockBehavior : SubsystemBlockBehavior
{
    public const int MaxMagnets = 8;

    private DynamicArray<Vector3> _magnets = [];

    private SubsystemPlayers _subsystemPlayers = null!;

    public override int[] HandledBlocks => [167];

    public int MagnetsCount => _magnets.Count;

    public Vector3 FindNearestCompassTarget(Vector3 compassPosition)
    {
        if (_magnets.Count > 0)
        {
            var num = float.MaxValue;
            var v = Vector3.Zero;
            for (var i = 0; i < _magnets.Count && i < 8; i++)
            {
                var vector = _magnets.Array[i];
                var num2 = Vector3.DistanceSquared(compassPosition, vector);
                if (!(num2 < num))
                {
                    continue;
                }

                num = num2;
                v = vector;
            }

            return v + new Vector3(0.5f);
        }

        var num3 = float.MaxValue;
        var v2 = Vector3.Zero;
        foreach (var playersDatum in _subsystemPlayers.PlayersData)
        {
            var spawnPosition = playersDatum.SpawnPosition;
            var num4 = Vector3.DistanceSquared(compassPosition, spawnPosition);
            if (!(num4 < num3))
            {
                continue;
            }

            num3 = num4;
            v2 = spawnPosition;
        }

        return v2 + new Vector3(0.5f);
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true)!;
        var value = valuesDictionary.GetValue<string>("Magnets");
        _magnets = new DynamicArray<Vector3>(HumanReadableConverter.ValuesListFromString<Vector3>(';', value));
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        base.Save(valuesDictionary);
        var value = HumanReadableConverter.ValuesListToString(';', _magnets.ToArray());
        valuesDictionary.SetValue("Magnets", value);
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
    {
        _magnets.Add(new Vector3(x, y, z));
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        _magnets.Remove(new Vector3(x, y, z));
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var cellContents = SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z);
        if (BlocksManager.Blocks[cellContents].Transparent)
        {
            SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
        }
    }
}
