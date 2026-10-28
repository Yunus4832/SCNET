using Engine.Core.Security;

using EntitySystem.Core;

using Game.Managers;
using Game.Network;
using Game.Protocol.Identity;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Subsystems;

namespace Survivalcraft.Test.Network;

[Collection(WorldRoleCollection.Name)]
public sealed class PlayerSessionQueryTest
{
    [Fact]
    public void TrustedRoleMetadataCanInitializeControlWithoutCreatingAPlayerRecord()
    {
        var previous = CommonLib.Net.Clients;
        var previousType = GameManager.Role;
        using var project = new QueryProject();
        try
        {
            CommonLib.Net.Role = WorldRole.RemoteClient;
            var address = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
            var session = new Client(null, 1, Guid.NewGuid(), Guid.Empty, project) { PlayerAddress = address };
            CommonLib.Net.Clients = new Dictionary<int, Client> { [1] = session };
            var role = Guid.NewGuid();
            PlayerSessionBinding.BindInitialCharacter(address, role);
            Assert.Equal(role, session.CharacterId);
            Assert.Null(session.Subject);
            Assert.Empty(project.Players.Store.Entries);
            Assert.Equal(address, PlayerSessionQuery.GetController(role));
        }
        finally
        {
            CommonLib.Net.Clients = previous;
            CommonLib.Net.Role = previousType;
        }
    }

    [Fact]
    public void InitialMetadataCannotReplaceAnExistingControlAssignment()
    {
        var previous = CommonLib.Net.Clients;
        var previousType = GameManager.Role;
        try
        {
            CommonLib.Net.Role = WorldRole.RemoteClient;
            var address = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
            var role = Guid.NewGuid();
            var session = new Client(null, 1, Guid.NewGuid(), role, null) { PlayerAddress = address };
            CommonLib.Net.Clients = new Dictionary<int, Client> { [1] = session };
            Assert.Throws<InvalidDataException>(() => PlayerSessionBinding.BindInitialCharacter(address, Guid.NewGuid())
            );
            Assert.Equal(role, session.CharacterId);
        }
        finally
        {
            CommonLib.Net.Clients = previous;
            CommonLib.Net.Role = previousType;
        }
    }

    private sealed class QueryProject : Project
    {
        public SubsystemPlayers Players { get; } = new();

        public QueryProject() => subsystems.Add(Players);
    }
}
