using Game.Network.Packages;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Network;

public sealed class ComponentHealthPackageTest
{
    [Fact]
    public void SyncHealthRoundTripsDeathCause()
    {
        var package = new ComponentHealthPackage
        {
            Type = ComponentHealthPackage.EventType.SyncHealth,
            TargetId = 42,
            Health = 0f,
            Cause = "Drowned"
        };

        var writer = new PackageStreamWriter();
        package.WriteData(writer);
        var clone = new ComponentHealthPackage();
        clone.ReadData(new PackageStreamReader(writer.Data()));

        Assert.Equal(ComponentHealthPackage.EventType.SyncHealth, clone.Type);
        Assert.Equal(42, clone.TargetId);
        Assert.Equal(0f, clone.Health);
        Assert.Equal("Drowned", clone.Cause);
    }
}
