using Game.Network;
using Game.Network.Packages;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Network;

public class SubsystemSeasonPackageTest
{
    [Fact]
    public void RoundTripPreservesTimeOfYear()
    {
        var package = new SubsystemSeasonPackage(0.625f);
        var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var clone = new SubsystemSeasonPackage();

        clone.ReadData(reader);

        Assert.Equal(0.625f, clone.TimeOfYear);
    }

    [Fact]
    public void UsesReliableControlTransport()
    {
        var transport = PackageTransportPolicy.Get(new SubsystemSeasonPackage(0.25f));

        Assert.Equal(PackageTransportPolicy.Control, transport);
    }
}
