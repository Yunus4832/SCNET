using System.Runtime.CompilerServices;

using Engine.Core.Security;

using Game.Characters;
using Game.Commands;
using Game.Managers;
using Game.Network;
using Game.Protocol.Identity;
using Game.World;

using Survivalcraft.Test.Subsystems;

namespace Survivalcraft.Test.Commands;

[Collection(WorldRoleCollection.Name)]
public sealed class CommandInputPrincipalTest
{
    [Theory]
    [InlineData(WorldRole.RemoteClient)]
    [InlineData(WorldRole.SinglePlayer)]
    [InlineData(WorldRole.Server)]
    public void OnlyRemoteClientInputCanUseMetadataWithoutAnAuthoritativeSubject(WorldRole worldRole)
    {
        var previousType = GameManager.Role;
        var previousClients = CommonLib.Net.Clients;
        var player = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        player.CharacterId = Guid.NewGuid();
        player.Name = "RemoteInput";
        var session = new Client(null, 1, Guid.NewGuid(), player.CharacterId, null)
        {
            PlayerAddress = new PlayerAddress(IdentityAddress.Encode(new byte[32]))
        };
        try
        {
            CommonLib.Net.Role = worldRole;
            CommonLib.Net.Clients = new Dictionary<int, Client> { [1] = session };
            Assert.Null(session.Subject);
            Assert.Throws<InvalidOperationException>(() => CommandPrincipal.FromPlayer(player));
            if (worldRole != WorldRole.RemoteClient)
            {
                Assert.Throws<InvalidOperationException>(() => CommandPrincipal.ForInput(player));
                return;
            }

            var principal = CommandPrincipal.ForInput(player);
            Assert.Equal(CommandPrincipalKind.Player, principal.Kind);
            Assert.Same(player, principal.Character);
            Assert.Null(principal.Subject);
            Assert.Empty(principal.Permissions);
            Assert.Empty(principal.DelegablePermissions);
            var registry = new CommandRegistry();
            BuiltInCommands.Register(registry, new ModId("game"));
            var adapter = new TextCommandAdapter(registry);
            Assert.Contains(adapter.Suggest("/team", principal), item => item.Value == "team");
            Assert.True(
                adapter.TryCreate(
                    "/team create InputFixture",
                    new CommandContext(CommandInvocationChannel.Text, principal, null),
                    out var command,
                    out _
                )
            );
            Assert.IsType<CreateTeamCommand>(command);
            Assert.True(CommandGateway.TryCreateRequest(registry, command!, "client-input", out _, out _));
        }
        finally
        {
            CommonLib.Net.Clients = previousClients;
            CommonLib.Net.Role = previousType;
        }
    }
}
