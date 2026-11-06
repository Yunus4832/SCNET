using Engine.Core.Security;

using EntitySystem.Core;

using Game.Characters;
using Game.Network;
using Game.Protocol.Identity;
using Game.Subsystems;

using Survivalcraft.Test.Subsystems;

namespace Survivalcraft.Test.Teams;

[Collection(NodeRoleCollection.Name)]
public sealed class CharacterTeamQueryTest
{
    [Fact]
    public void BorrowedCharactersFollowControllersForTeamViewsWithoutMovingOwnership()
    {
        using var project = new TeamProject();
        var oldClients = project.Node.Clients.Values.ToArray();
        try
        {
            var first = project.Players.Store.GetOrCreate(Address(1));
            var second = project.Players.Store.GetOrCreate(Address(2));
            var firstRole = Guid.NewGuid();
            var secondRole = Guid.NewGuid();
            project.Players.Store.BindCharacter(first.Address, firstRole);
            project.Players.Store.BindCharacter(second.Address, secondRole);
            project.Characters.ApplyCharacterList(
                [
                    new CharacterListEntry(firstRole, first.Address, "First", true),
                    new CharacterListEntry(secondRole, second.Address, "Second", true)
                ]
            );
            project.Teams.Store.TryCreate(first, "One", out _);
            project.Teams.Store.TryCreate(second, "Two", out _);
            Fixtures.ConnectionTestEnvironment.SetRoster(
                project.Node,
                (new Dictionary<int, Client>
                {
                    [1] = new Client(null, 1, Guid.NewGuid(), secondRole) { PlayerAddress = first.Address },
                    [2] = new Client(null, 2, Guid.NewGuid(), firstRole) { PlayerAddress = second.Address }
                }).Values
            );
            Assert.Equal(firstRole, Assert.Single(project.Characters.GetOwnedCharacters(first.Address)).CharacterId);
            Assert.Equal(
                secondRole,
                Assert.Single(CharacterTeamQuery.GetControlledTeamCharacters(project, first.Address)).CharacterId
            );
            Assert.Equal(
                project.Teams.Store.FindForPlayer(second.Address)!.Id,
                CharacterTeamQuery.GetTeamId(project, firstRole)
            );
            if (project.Node.Clients.TryGetValue(1, out var removedConnection))
            {
                project.Node.UnregisterClient(removedConnection);
            }

            Assert.Empty(CharacterTeamQuery.GetControlledTeamCharacters(project, first.Address));
            Assert.NotNull(project.Teams.Store.FindForPlayer(first.Address));
            Assert.Equal(Guid.Empty, CharacterTeamQuery.GetTeamId(project, secondRole));
        }
        finally { Fixtures.ConnectionTestEnvironment.SetRoster(project.Node, oldClients); }
    }

    private static PlayerAddress Address(byte value)
    {
        var bytes = new byte[32];
        bytes[0] = value;
        return new PlayerAddress(IdentityAddress.Encode(bytes));
    }

    private sealed class TeamProject : Project
    {
        public SubsystemPlayers Players { get; } = new();

        public SubsystemCharacters Characters { get; } = new();

        public SubsystemPositionMarks Marks { get; } = new();

        public SubsystemTeams Teams { get; }

        public NetNode Node { get; } = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );

        public TeamProject()
        {
            Node.Runtime.BindProject(this);
            Fixtures.RuleHookTestEnvironment.BindEmpty(this);
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
}
