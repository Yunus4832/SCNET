using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game;
using Game.Modding;
using Game.Network;
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
            new ModDescriptor(new ModManifest(ModEntry.ModId, "Territory", "1.0.0"), static () => new ModEntry())
        );
        var index = scope.Catalog.GetBlock(TerritoryBlock.Id).BlockIndex;
        scope.Catalog.Blocks[index] = new TerritoryBlock { Collidable = true };
        Assert.False(SubsystemPistonBlockBehavior.IsBlockMovable(Terrain.MakeBlockValue(index), 0, 64, out var isEnd));
        Assert.False(isEnd);
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

            Assert.True(TerritoryProtection.CanDig(territory, owner));
            Assert.True(TerritoryProtection.CanPlace(territory, administrator));
            Assert.False(TerritoryProtection.CanDig(territory, stranger));

            Assert.False(TerritoryProtection.CanDig(territory, stranger));
            Assert.False(TerritoryProtection.AllowsOwner(territory, stranger));
        }
        finally
        {
            CommonLib.WorkType = previousWorkType;
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void NonMemberDiggingAndPlacingAreIndependent(bool digging, bool placing)
    {
        var territory = new Territory(
            Guid.NewGuid(),
            new Point3(0, 64, 0),
            new TerritorySettings(AllowDigging: digging, AllowPlacing: placing)
        );
        var player = Player(Guid.NewGuid());
        Assert.Equal(digging, TerritoryProtection.CanDig(territory, player));
        Assert.Equal(placing, TerritoryProtection.CanPlace(territory, player));
        Assert.True(TerritoryProtection.CanDig(territory, null));
        Assert.True(TerritoryProtection.CanPlace(territory, null));
    }

    [Fact]
    public void TeamMembershipDoesNotGrantTerritoryPermissions()
    {
        var owner = Guid.NewGuid();
        var teammate = Player(Guid.NewGuid());
        var project = new TeamProject();
        var group = new SubsystemPlayers.Group();
        group.Members.Add(owner);
        group.Members.Add(teammate.PlayerGUID);
        project.Players.ServerGroups.Add("team", group);
        teammate.GroupKey = "team";
        typeof(PlayerData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(teammate, project);
        var restricted = new Territory(owner, new Point3(0, 64, 0));
        Assert.False(TerritoryProtection.CanDig(restricted, teammate));
        Assert.False(TerritoryProtection.CanPlace(restricted, teammate));
        restricted.SetGrant(teammate.PlayerGUID, "Teammate", TerritoryPermission.Dig);
        Assert.True(TerritoryProtection.CanDig(restricted, teammate));
        Assert.False(TerritoryProtection.CanPlace(restricted, teammate));
        Assert.False(TerritoryProtection.AllowsOwner(restricted, teammate));
    }

    [Theory]
    [InlineData(TerritoryPermission.Enter, true, false, false, false)]
    [InlineData(TerritoryPermission.Exit, false, true, false, false)]
    [InlineData(TerritoryPermission.Dig, false, false, true, false)]
    [InlineData(TerritoryPermission.Place, false, false, false, true)]
    public void PersonalGrantsAreIndependentAndNeverGrantOwnership(
        TerritoryPermission permissions,
        bool enter,
        bool exit,
        bool dig,
        bool place
    )
    {
        var previous = CommonLib.WorkType;
        CommonLib.WorkType = WorkType.Local;
        try
        {
            var territory = new Territory(
                Guid.NewGuid(),
                new Point3(0, 64, 0),
                new TerritorySettings(AllowExit: false)
            );
            var player = Player(Guid.NewGuid());
            territory.SetGrant(player.PlayerGUID, "Visitor", permissions);
            Assert.Equal(enter, TerritoryProtection.CanCross(territory, player, false));
            Assert.Equal(exit, TerritoryProtection.CanCross(territory, player, true));
            Assert.Equal(dig, TerritoryProtection.CanDig(territory, player));
            Assert.Equal(place, TerritoryProtection.CanPlace(territory, player));
            Assert.False(TerritoryProtection.AllowsOwner(territory, player));
        }
        finally
        {
            CommonLib.WorkType = previous;
        }
    }

    [Fact]
    public void RevokedAccessStillAllowsLeavingWithoutAnEntryCollision()
    {
        var previous = CommonLib.WorkType;
        CommonLib.WorkType = WorkType.Local;
        try
        {
            var territory = new Territory(Guid.NewGuid(), new Point3(0, 64, 0));
            var player = Player(Guid.NewGuid());
            territory.SetGrant(player.PlayerGUID, "Visitor", TerritoryPermission.Enter);
            territory.SetGrant(player.PlayerGUID, "Visitor", TerritoryPermission.None);
            Assert.True(TerritoryProtection.CanCross(territory, player, true));
            Assert.False(TerritoryProtection.CanCross(territory, player, false));
            var position = new Vector3(territory.MaxX + 0.1f, 64, 0);
            var body = new BoundingBox(
                position - new Vector3(0.3f, 0, 0.3f),
                position + new Vector3(0.3f, 1.8f, 0.3f)
            );
            Assert.Contains(territory.GetBoundaryBoxes(63, 67), wall => wall.Intersection(body));
            Assert.DoesNotContain(
                territory.GetBlockingBoundaryBoxes(63, 67, Vector3.UnitX, false),
                wall => wall.Intersection(body)
            );
            Assert.Single(
                territory.GetBlockingBoundaryBoxes(63, 67, -Vector3.UnitX, false),
                wall => wall.Intersection(body)
            );
        }
        finally
        {
            CommonLib.WorkType = previous;
        }
    }

    private sealed class TeamProject : Project
    {
        public SubsystemPlayers Players { get; } = new();

        public TeamProject() => subsystems.Add(Players);
    }

    private static PlayerData Player(Guid guid)
    {
        var player = (PlayerData)RuntimeHelpers.GetUninitializedObject(typeof(PlayerData));
        player.PlayerGUID = guid;
        player.GroupKey = string.Empty;
        return player;
    }
}
