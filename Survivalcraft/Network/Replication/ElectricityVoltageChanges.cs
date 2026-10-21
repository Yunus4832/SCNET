namespace Game.Network.Replication;

internal sealed record ElectricityVoltageChanges(
    bool IsBaseline,
    IReadOnlyDictionary<Point3, float> Voltages,
    IReadOnlyList<Point3> Removed
);
