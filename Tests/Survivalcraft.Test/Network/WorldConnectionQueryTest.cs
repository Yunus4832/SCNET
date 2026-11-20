using Engine.Core.Security;

using Game.Network;
using Game.Protocol.Identity;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Network;

public sealed class WorldConnectionQueryTest
{
    [Fact]
    public void TrustedCharacterMetadataCanInitializeControlWithoutCreatingAPlayerRecord()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var players = new SubsystemPlayers();
        project.AddSubsystem(players);
        var address = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
        var session = AddSession(project, address, Guid.Empty);
        var characterId = Guid.NewGuid();

        WorldConnectionControl.BindInitialCharacter(project, address, characterId);

        Assert.Equal(characterId, session.CharacterId);
        Assert.Null(Game.Players.WorldConnectionResolver.FindPlayer(session));
        Assert.Empty(players.Store.Entries);
        Assert.Equal(address, WorldConnectionQuery.GetController(project, characterId));
    }

    [Fact]
    public void InitialMetadataCannotReplaceAnExistingControlAssignment()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var address = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
        var characterId = Guid.NewGuid();
        var session = AddSession(project, address, characterId);

        Assert.Throws<InvalidDataException>(() =>
            WorldConnectionControl.BindInitialCharacter(project, address, Guid.NewGuid())
        );
        Assert.Equal(characterId, session.CharacterId);
    }

    [Fact]
    public void QueriesAndInitialBindingsCannotCrossWorldsOrClosedEpochs()
    {
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var address = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
        var authoritativeCharacter = Guid.NewGuid();
        var clientCharacter = Guid.NewGuid();
        var authoritativeSession = AddSession(authority, address, authoritativeCharacter);
        var clientSession = AddSession(client, address, clientCharacter);
        var clientNode = WorldNetworkBinding.For(client).RequireNode();
        clientNode.Self = clientSession;

        Assert.Same(authoritativeSession, WorldConnectionQuery.Find(authority, address));
        Assert.Same(clientSession, WorldConnectionQuery.Find(client, address));
        Assert.Null(WorldConnectionQuery.GetController(authority, clientCharacter));
        Assert.Null(WorldConnectionQuery.GetController(client, authoritativeCharacter));
        Assert.Equal(
            authoritativeCharacter,
            Assert.Single(
                WorldConnectionQuery.GetControlledCharacterIds(authority, new HashSet<PlayerAddress> { address })
            )
        );
        Assert.Equal(
            clientCharacter,
            Assert.Single(
                WorldConnectionQuery.GetControlledCharacterIds(client, new HashSet<PlayerAddress> { address })
            )
        );
        Assert.False(WorldConnectionQuery.IsLocalCharacter(authority, authoritativeCharacter));
        Assert.True(WorldConnectionQuery.IsLocalCharacter(client, clientCharacter));
        WorldConnectionControl.BindInitialCharacter(authority, address, Guid.NewGuid());
        Assert.Equal(authoritativeCharacter, authoritativeSession.CharacterId);
        clientNode.Runtime.Close();
        clientNode.PrepareWorldRuntime();
        clientSession.SetControlledCharacter(Guid.NewGuid());
        var nextCharacter = clientSession.CharacterId;
        WorldConnectionControl.BindInitialCharacter(client, address, Guid.NewGuid());
        Assert.Equal(nextCharacter, clientSession.CharacterId);
        client.Dispose();
        WorldConnectionControl.BindInitialCharacter(client, address, Guid.NewGuid());
        Assert.Equal(nextCharacter, clientSession.CharacterId);

        Assert.Null(WorldConnectionQuery.Find(client, address));
        Assert.False(WorldConnectionQuery.IsLocalCharacter(client, clientSession.CharacterId));
        Assert.Empty(WorldConnectionQuery.GetControlledCharacterIds(client, new HashSet<PlayerAddress> { address }));
        Assert.Same(authoritativeSession, WorldConnectionQuery.Find(authority, address));
        clientNode.Runtime.Close();
    }

    private static Client AddSession(RuntimeTestProject project, PlayerAddress address, Guid characterId)
    {
        var session = new Client(null, 1, Guid.NewGuid(), characterId) { PlayerAddress = address };
        WorldNetworkBinding.For(project).RequireNode().AddClient(session);
        return session;
    }
}
