namespace Survivalcraft.Test.Network;

public sealed class ComponentHealthPackageTest
{
    [Fact]
    public void SyncHealthRoundTripsDeathCause()
    {
        var package = new EntityHealthPackage { TargetId = 42, Health = 0f, Cause = "Drowned" };

        var writer = ProtocolMessageCodec.Encode(package);
        var clone = Assert.IsType<EntityHealthPackage>(ProtocolMessageCodec.Decode(writer));

        Assert.Equal(ComponentHealthPackage.EventType.SyncHealth, clone.Type);
        Assert.Equal(42, clone.TargetId);
        Assert.Equal(0f, clone.Health);
        Assert.Equal("Drowned", clone.Cause);
    }
}
