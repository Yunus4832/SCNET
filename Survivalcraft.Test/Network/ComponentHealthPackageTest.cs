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

        var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(package, writer);
        var clone = Assert.IsType<ComponentHealthPackage>(ProtocolMessageCodec.Read(package.ID, new ProtocolReader(writer.Data())));

        Assert.Equal(ComponentHealthPackage.EventType.SyncHealth, clone.Type);
        Assert.Equal(42, clone.TargetId);
        Assert.Equal(0f, clone.Health);
        Assert.Equal("Drowned", clone.Cause);
    }
}
