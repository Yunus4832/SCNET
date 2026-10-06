using Game.Network;
using Game.Network.Packages.Handlers;

namespace Survivalcraft.Test.Network;

public sealed class TerritoryRequestAuthorityTest
{
    [Fact]
    public void OnlyAuthenticatedOwnerMayChangeTerritorySettings()
    {
        var ownerGuid = Guid.NewGuid();
        var owner = new Client(null, 1, Guid.NewGuid(), ownerGuid, null);
        var other = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);

        Assert.True(TerritoriyPackageHandler.CanApplyRequest(owner, ownerGuid));
        Assert.False(TerritoriyPackageHandler.CanApplyRequest(other, ownerGuid));
        Assert.False(TerritoriyPackageHandler.CanApplyRequest(null, ownerGuid));
        Assert.False(TerritoriyPackageHandler.CanApplyRequest(owner, Guid.Empty));
    }
}
