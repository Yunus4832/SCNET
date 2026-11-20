using Engine.Core.Mathematics;

using Game.Subsystems;

namespace Survivalcraft.Test.Network;

internal static class ElectricityTestSnapshots
{
    internal static ElectricitySnapshot Capture(SubsystemElectricity.NetSimulate snapshot) => new()
    {
        StartStep = snapshot.StartStep,
        IsBaseline = snapshot.IsBaseline,
        SaveData = new Dictionary<Point3, float>(snapshot.SaveData),
        Removed = snapshot.Removed.ToArray()
    };
}
