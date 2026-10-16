using System.Runtime.CompilerServices;

using Engine.Core;

using Game;
using Game.Managers;
using Game.Modding;
using Game.Network;
using Game.Network.Enums;
using Game.Subsystems;
using Game.Terrains;

using TerritoryStoneMod;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public class TerritoryProtectionTest
{
    [Fact]
    public void PistonUsesTheModBlocksMovabilityContractWithoutAKnownIndexCase()
    {
        using var scope = new WorldCatalogScope(
            new ModDescriptor(new ModManifest(ModEntry.ModId, "Territory", "1.0.0"), static () => new ModEntry()));
        var index = scope.Catalog.GetBlock(TerritoryBlock.Id).BlockIndex;
        try
        {
            scope.Catalog.Blocks[index] = new TerritoryBlock { Collidable = true };
            Assert.False(SubsystemPistonBlockBehavior.IsBlockMovable(
                Terrain.MakeBlockValue(index), 0, 64, out var isEnd));
            Assert.False(isEnd);
        }
        finally
        {

        }
    }

    [Fact]
    public void OwnerAndAdministratorAreAllowedButUnrelatedPlayerIsDenied()
    {
        var previousWorkType = CommonLib.WorkType;
        CommonLib.WorkType = WorkType.Local;
        try
        {
            var territory = new Territory(Guid.NewGuid(), new Point3(0, 64, 0));
            var owner = Player(territory.Owner);
            var stranger = Player(Guid.NewGuid());
            var administrator = Player(Guid.NewGuid());
            administrator.ServerManager = true;

            Assert.True(TerritoryProtection.Allows(territory, owner, false));
            Assert.True(TerritoryProtection.Allows(territory, administrator, false));
            Assert.False(TerritoryProtection.Allows(territory, stranger, true));
            territory.RestrictEntry = false;
            Assert.False(TerritoryProtection.Allows(territory, stranger, true));
            Assert.False(TerritoryProtection.AllowsOwner(territory, stranger));
        }
        finally
        {
            CommonLib.WorkType = previousWorkType;
        }
    }

    [Fact]
    public void AnonymousEnvironmentalActionPolicyMustBeChosenByEachOperation()
    {
        var territory = new Territory(Guid.NewGuid(), new Point3(0, 64, 0));
        Assert.True(TerritoryProtection.Allows(territory, null, true));
        Assert.False(TerritoryProtection.Allows(territory, null, false));
        Assert.False(TerritoryProtection.AllowsOwner(territory, null));
    }

    private static PlayerData Player(Guid guid)
    {
        var player = (PlayerData)RuntimeHelpers.GetUninitializedObject(typeof(PlayerData));
        player.PlayerGUID = guid;
        player.GroupKey = string.Empty;
        return player;
    }
}
