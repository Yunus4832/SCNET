using Game.Network.Packages;
using Game.Network.Packages.Handlers;

namespace Survivalcraft.Test.Network;

public sealed class SleepPackageDirectionTest
{
    [Theory]
    [InlineData(ComponentSleepPackage.EventType.SleepRequest, true, false)]
    [InlineData(ComponentSleepPackage.EventType.WakeupRequest, true, false)]
    [InlineData(ComponentSleepPackage.EventType.Sleep, false, true)]
    [InlineData(ComponentSleepPackage.EventType.WakeUp, false, true)]
    public void RequestsAndAuthoritativeStatesHaveDistinctDirections(ComponentSleepPackage.EventType type,
        bool server, bool client)
    {
        Assert.Equal(server, ComponentSleepPackageHandler.AcceptsDirection(type, true));
        Assert.Equal(client, ComponentSleepPackageHandler.AcceptsDirection(type, false));
    }
}
