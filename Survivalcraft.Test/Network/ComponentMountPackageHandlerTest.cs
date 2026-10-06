using Game.Network.Packages;
using Game.Network.Packages.Handlers;

namespace Survivalcraft.Test.Network;

public sealed class ComponentMountPackageHandlerTest
{
    [Theory]
    [InlineData(ComponentMountPackage.EventType.Mount, false)]
    [InlineData(ComponentMountPackage.EventType.Dismount, false)]
    [InlineData(ComponentMountPackage.EventType.MountRequest, true)]
    [InlineData(ComponentMountPackage.EventType.DismountRequest, true)]
    public void RequestsAndAuthoritativeEventsHaveOppositeDirections(ComponentMountPackage.EventType type,
        bool acceptedByServer)
    {
        Assert.Equal(acceptedByServer, ComponentMountPackageHandler.AcceptsDirection(type, true));
        Assert.Equal(!acceptedByServer, ComponentMountPackageHandler.AcceptsDirection(type, false));
    }

    [Fact]
    public void UnknownEventIsRejectedInBothDirections()
    {
        var type = (ComponentMountPackage.EventType)255;
        Assert.False(ComponentMountPackageHandler.AcceptsDirection(type, true));
        Assert.False(ComponentMountPackageHandler.AcceptsDirection(type, false));
    }
}
