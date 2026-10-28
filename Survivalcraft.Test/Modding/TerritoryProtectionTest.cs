using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Characters;
using Game.Managers;
using Game.Modding;
using Game.Network;
using Game.Protocol.Identity;
using Game.Subsystems;
using Game.Teams;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Subsystems;

using TerritoryStoneMod;

using static Survivalcraft.Test.Modding.TerritoryTestIdentity;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public class TerritoryProtectionTest : IDisposable
{
    private readonly Dictionary<int, Client> _previousClients = CommonLib.Net.Clients;

    private readonly TeamProject _project = new();

    public TerritoryProtectionTest()
    {
        CommonLib.Net.Clients = [];
    }

    public void Dispose()
    {
        CommonLib.Net.Clients = _previousClients;
        _project.Dispose();
    }

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
        var previousWorldRole = GameManager.Role;
        CommonLib.Net.Role = WorldRole.SinglePlayer;
        try
        {
            var territory = new Territory(Address(), new Point3(0, 64, 0));
            var owner = Player(territory.Owner);
            var stranger = Player(Address());
            var administrator = Player(Address());
            administrator.Client!.Subject!.Permissions.Grant(TerritoryProtection.AdminPermission, false);

            Assert.True(TerritoryProtection.CanDig(territory, owner));
            Assert.True(TerritoryProtection.CanPlace(territory, administrator));
            Assert.False(TerritoryProtection.CanDig(territory, stranger));

            Assert.False(TerritoryProtection.CanDig(territory, stranger));
            Assert.False(TerritoryProtection.AllowsOwner(territory, stranger));
        }
        finally
        {
            CommonLib.Net.Role = previousWorldRole;
        }
    }

    [Fact]
    public void RoleOwnershipAndLocalCharacterStatusDoNotGrantSubjectPermissions()
    {
        var owner = Address();
        var territory = new Territory(owner, new Point3(0, 64, 0));
        var controlled = Player(Address());
        controlled.OwnerAddress = owner;
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(controlled, true);
        Assert.False(TerritoryProtection.AllowsOwner(territory, controlled));
        Assert.False(TerritoryProtection.CanDig(territory, controlled));
        var actor = controlled.Client!.Subject!;
        territory.SetGrant(actor.Address, "Actual actor", TerritoryPermission.Dig);
        Assert.True(TerritoryProtection.CanDig(territory, controlled));
        Assert.False(TerritoryProtection.CanPlace(territory, controlled));
        actor.Permissions.Grant(TerritoryProtection.AdminPermission, false);
        Assert.True(TerritoryProtection.CanPlace(territory, controlled));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void NonMemberDiggingAndPlacingAreIndependent(bool digging, bool placing)
    {
        var territory = new Territory(
            Address(),
            new Point3(0, 64, 0),
            new TerritorySettings(AllowDigging: digging, AllowPlacing: placing)
        );
        var player = Player(Address());
        Assert.Equal(digging, TerritoryProtection.CanDig(territory, player));
        Assert.Equal(placing, TerritoryProtection.CanPlace(territory, player));
        Assert.True(TerritoryProtection.CanDig(territory, null));
        Assert.True(TerritoryProtection.CanPlace(territory, null));
    }

    [Fact]
    public void TeamMembershipDoesNotGrantTerritoryPermissions()
    {
        var owner = Address();
        var teammate = Player(Address());
        _project.Players.Store.GetOrCreate(owner);
        _project.Teams.Store.Replace(
            [new Team(Guid.NewGuid(), "Team", owner, [owner, teammate.Client!.PlayerAddress!.Value])]
        );
        var restricted = new Territory(owner, new Point3(0, 64, 0));
        Assert.False(TerritoryProtection.CanDig(restricted, teammate));
        Assert.False(TerritoryProtection.CanPlace(restricted, teammate));
        restricted.SetGrant(teammate.Client!.PlayerAddress!.Value, "Teammate", TerritoryPermission.Dig);
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
        var previous = GameManager.Role;
        CommonLib.Net.Role = WorldRole.SinglePlayer;
        try
        {
            var territory = new Territory(
                Address(),
                new Point3(0, 64, 0),
                new TerritorySettings(AllowExit: false)
            );
            var player = Player(Address());
            territory.SetGrant(player.Client!.PlayerAddress!.Value, "Visitor", permissions);
            Assert.Equal(enter, TerritoryProtection.CanCross(territory, player, false));
            Assert.Equal(exit, TerritoryProtection.CanCross(territory, player, true));
            Assert.Equal(dig, TerritoryProtection.CanDig(territory, player));
            Assert.Equal(place, TerritoryProtection.CanPlace(territory, player));
            Assert.False(TerritoryProtection.AllowsOwner(territory, player));
        }
        finally
        {
            CommonLib.Net.Role = previous;
        }
    }

    [Fact]
    public void RevokedAccessStillAllowsLeavingWithoutAnEntryCollision()
    {
        var previous = GameManager.Role;
        CommonLib.Net.Role = WorldRole.SinglePlayer;
        try
        {
            var territory = new Territory(Address(), new Point3(0, 64, 0));
            var player = Player(Address());
            territory.SetGrant(player.Client!.PlayerAddress!.Value, "Visitor", TerritoryPermission.Enter);
            territory.SetGrant(player.Client!.PlayerAddress!.Value, "Visitor", TerritoryPermission.None);
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
            CommonLib.Net.Role = previous;
        }
    }

    private sealed class TeamProject : Project
    {
        public SubsystemPlayers Players { get; } = new();

        public SubsystemCharacters Characters { get; } = new();

        public SubsystemPositionMarks Marks { get; } = new();

        public SubsystemTeams Teams { get; }

        public TeamProject()
        {
            Teams = TeamTestSupport.CreateSubsystem(Players.Store);
            subsystems.Add(Players);
            subsystems.Add(Characters);
            subsystems.Add(Marks);
            WorldSubsystemTestSupport.Attach(this, Players);
            WorldSubsystemTestSupport.Attach(this, Characters);
            WorldSubsystemTestSupport.Attach(this, Marks);
            subsystems.Add(Teams);
        }
    }

    private CharacterData Player(PlayerAddress address)
    {
        var player = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        player.CharacterId = Guid.NewGuid();
        player.OwnerAddress = address;
        player.SubsystemCharacters = _project.Characters;
        typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(player, _project);
        _project.Players.Store.GetOrCreate(address);
        var id = (byte)(CommonLib.Net.Clients.Count + 1);
        CommonLib.Net.Clients[id] = new Client(null, id, Guid.NewGuid(), player.CharacterId, _project)
            { PlayerAddress = address };
        return player;
    }
}
