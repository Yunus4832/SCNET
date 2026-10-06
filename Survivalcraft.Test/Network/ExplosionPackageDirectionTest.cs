using Game.Network.Packages.Handlers;

namespace Survivalcraft.Test.Network;

public sealed class ExplosionPackageDirectionTest
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ExplosionEffectsAreOnlyAcceptedOnClients(bool isServer, bool accepted)
    {
        Assert.Equal(accepted, ExplosionsPackageHandler.AcceptsDirection(isServer));
    }
}
