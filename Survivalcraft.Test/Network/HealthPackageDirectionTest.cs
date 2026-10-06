using Game.Network.Packages;
using Game.Network.Packages.Handlers;

namespace Survivalcraft.Test.Network;

public sealed class HealthPackageDirectionTest
{
    [Theory]
    [InlineData(ComponentHealthPackage.EventType.RequestInjure, true, false)]
    [InlineData(ComponentHealthPackage.EventType.Injure, false, true)]
    [InlineData(ComponentHealthPackage.EventType.HitResult, false, true)]
    [InlineData(ComponentHealthPackage.EventType.SyncHealth, false, true)]
    [InlineData(ComponentHealthPackage.EventType.Damage, false, true)]
    public void OnlyServerCanPublishHealthState(ComponentHealthPackage.EventType type, bool server, bool client)
    {
        Assert.Equal(server, ComponentHealthPackageHandler.AcceptsDirection(type, true));
        Assert.Equal(client, ComponentHealthPackageHandler.AcceptsDirection(type, false));
    }
}
