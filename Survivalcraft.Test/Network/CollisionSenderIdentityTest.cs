using Game.Network;
using Game.Network.Packages.Handlers;

namespace Survivalcraft.Test.Network;

public sealed class CollisionSenderIdentityTest
{
    [Fact]
    public void ReusedClientNumberAndIdentityDoNotGrantOldConnectionOwnership()
    {
        var identity = Guid.NewGuid();
        var token = Guid.NewGuid();
        var owner = new Client(null, 1, token, identity, null);
        var replacement = new Client(null, 1, token, identity, null);

        Assert.True(SubsystemBodyPackageHandler.OwnsCollisionSource(owner, owner));
        Assert.False(SubsystemBodyPackageHandler.OwnsCollisionSource(replacement, owner));
        Assert.False(SubsystemBodyPackageHandler.OwnsCollisionSource(owner, replacement));
    }

    [Fact]
    public void MissingSenderOrUnownedBodyCannotAuthorizeCollision()
    {
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);

        Assert.False(SubsystemBodyPackageHandler.OwnsCollisionSource(null, null));
        Assert.False(SubsystemBodyPackageHandler.OwnsCollisionSource(null, client));
        Assert.False(SubsystemBodyPackageHandler.OwnsCollisionSource(client, null));
    }
}
