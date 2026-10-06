using Game.Network.Packages;
using Game.Network.Packages.Handlers;

namespace Survivalcraft.Test.Network;

public sealed class SubsystemBodyPackageHandlerTest
{
    [Theory]
    [InlineData(SubsystemBodyPackage.EventType.BodyUpdate, false)]
    [InlineData(SubsystemBodyPackage.EventType.ApplyImpulse, false)]
    [InlineData(SubsystemBodyPackage.EventType.HandleAxisCollision, true)]
    public void ServerOnlyAcceptsCollisionReports(SubsystemBodyPackage.EventType type, bool expected)
    {
        Assert.Equal(expected, SubsystemBodyPackageHandler.AcceptsDirection(type, true));
    }

    [Theory]
    [InlineData(SubsystemBodyPackage.EventType.BodyUpdate)]
    [InlineData(SubsystemBodyPackage.EventType.ApplyImpulse)]
    [InlineData(SubsystemBodyPackage.EventType.HandleAxisCollision)]
    public void ClientAcceptsAuthoritativeBodyEvents(SubsystemBodyPackage.EventType type)
    {
        Assert.True(SubsystemBodyPackageHandler.AcceptsDirection(type, false));
    }
}
