using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public class SubsystemSeasonPackageTest
{
    [Fact]
    public void RoundTripPreservesTimeOfYear()
    {
        var package = new SubsystemSeasonPackage(0.625f);
        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = (SubsystemSeasonPackage)ProtocolMessageCodec.Decode(reader);

        Assert.Equal(0.625f, clone.TimeOfYear);
    }

    [Fact]
    public void UsesReliableControlTransport()
    {
        var transport = PackageTransportPolicy.Get(new SubsystemSeasonPackage(0.25f));

        Assert.Equal(PackageTransportPolicy.Control, transport);
    }
}
