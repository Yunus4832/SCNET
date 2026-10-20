using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public class SubsystemSeasonPackageTest
{
    [Fact]
    public void RoundTripPreservesTimeOfYear()
    {
        var package = new SubsystemSeasonPackage(0.625f);
        var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(package, writer);
        using var reader = new ProtocolReader(writer.Data());
        var clone = (SubsystemSeasonPackage)ProtocolMessageCodec.Read(package.ID, reader);

        Assert.Equal(0.625f, clone.TimeOfYear);
    }

    [Fact]
    public void UsesReliableControlTransport()
    {
        var transport = PackageTransportPolicy.Get(new SubsystemSeasonPackage(0.25f));

        Assert.Equal(PackageTransportPolicy.Control, transport);
    }
}
