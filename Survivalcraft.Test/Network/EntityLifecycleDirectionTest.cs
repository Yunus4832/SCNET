using Game.Network.Handling;

namespace Survivalcraft.Test.Network;

public sealed class EntityLifecycleDirectionTest
{
    [Theory]
    [InlineData(EntityPackage.EventType.LoadOne)]
    [InlineData(EntityPackage.EventType.LoadList)]
    [InlineData(EntityPackage.EventType.Remove)]
    public void AuthorityRejectsIncomingEntityLifecycleState(EntityPackage.EventType type)
    {
        Assert.False(EntityPackageHandler.AcceptsDirection(type, true));
        Assert.True(EntityPackageHandler.AcceptsDirection(type, false));
    }
}
