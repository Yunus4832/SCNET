using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Security;

using Game.Characters;
using Game.Commands;
using Game.Commands.BuiltIn;
using Game.Commands.Text;
using Game.Network;
using Game.Protocol.Identity;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Commands;

public sealed class CommandInputPrincipalTest
{
    [Theory]
    [InlineData(RuntimeSide.Client)]
    [InlineData(RuntimeSide.Authority)]
    public void OnlyClientInputCanUseMetadataWithoutAnAuthoritativeSubject(RuntimeSide side)
    {
        using var project = new RuntimeTestProject(side);
        project.AddSubsystem(new SubsystemGameInfo { WorldSettings = new WorldSettings() });
        var node = WorldNetworkBinding.For(project).RequireNode();
        var player = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(player, project);
        player.CharacterId = Guid.NewGuid();
        player.Name = "RemoteInput";
        var session = new Client(null, 1, Guid.NewGuid(), player.CharacterId)
        {
            PlayerAddress = new PlayerAddress(IdentityAddress.Encode(new byte[32]))
        };
        node.AddClient(session);
        Assert.Null(Game.Players.WorldConnectionResolver.FindPlayer(session));
        Assert.Throws<InvalidOperationException>(() => CommandPrincipal.FromPlayer(player));
        if (side == RuntimeSide.Authority)
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
        BuiltInCommandTestRegistry.Register(registry, new ModId("game"));
        var adapter = new TextCommandAdapter(registry, Game.Managers.GameManager.AuthorityProject);
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
}
