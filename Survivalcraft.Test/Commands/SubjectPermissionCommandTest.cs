using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using Engine.Core.Security;

using EntitySystem.Core;

using Game.Commands;
using Game.Managers;
using Game.Network;
using Game.Protocol.Identity;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Subsystems;

namespace Survivalcraft.Test.Commands;

[Collection(WorldRoleCollection.Name)]
public sealed class SubjectPermissionCommandTest
{
    [Fact]
    public void TextPlayersCommandListsOfflinePlayersWithoutCharacters()
    {
        var previous = GameManager.Role;
        try
        {
            CommonLib.Net.Role = WorldRole.Server;
            using var project = new SubjectProject();
            var address = Address("offline-text-command");
            project.Players.Store.GetOrCreate(address);
            var adapter = new TextCommandAdapter(Registry());
            var context = new CommandContext(CommandInvocationChannel.Text, CommandPrincipal.LocalOperator, project);
            Assert.True(adapter.TryCreate("/permission players", context, out var command, out _));
            Assert.IsType<ListPermissionPlayersCommand>(command);
            var result = adapter.Execute("/permission players", context);
            Assert.True(result.Success);
            Assert.Contains(address.Value, result.Message);
            Assert.Empty(project.Characters.Characters);
            Assert.True(
                adapter.Execute($"/permission grant {address.Value} game:player.teleport.self", context).Success
            );
            Assert.True(adapter.Execute($"/permission list {address.Value}", context).Success);
            Assert.True(
                adapter.Execute($"/permission revoke {address.Value} game:player.teleport.self", context).Success
            );
            Assert.False(adapter.TryCreate("/permission users", context, out _, out _));
        }
        finally
        {
            CommonLib.Net.Role = previous;
        }
    }

    [Fact]
    public void TextModerationUsesPlayerAddressesAndPlayerPermissions()
    {
        var previous = GameManager.Role;
        try
        {
            CommonLib.Net.Role = WorldRole.Server;
            using var project = new SubjectProject();
            var player = project.Players.Store.GetOrCreate(Address("text-moderation"));
            var registry = Registry();
            var adapter = new TextCommandAdapter(registry);
            var context = new CommandContext(CommandInvocationChannel.Console, CommandPrincipal.LocalOperator, project);
            Assert.True(adapter.Execute($"/player mute {player.Address}", context).Success);
            Assert.True(player.IsMuted);
            Assert.Contains(player.Address.Value, adapter.Execute("/player muted", context).Message);
            Assert.True(adapter.Execute($"/player unmute {player.Address}", context).Success);
            Assert.False(player.IsMuted);
            Assert.True(adapter.Execute($"/player ban {player.Address} testing", context).Success);
            Assert.NotNull(player.Ban);
            Assert.Contains(player.Address.Value, adapter.Execute("/player bans", context).Message);
            Assert.True(adapter.Execute($"/player unban {player.Address}", context).Success);
            Assert.Null(player.Ban);
            Assert.Empty(project.Characters.Characters);
            Assert.False(adapter.TryFind("user", out _));
            var permissions = registry.Permissions.Definitions.Select(entry => entry.Id.ToString()).ToArray();
            Assert.Contains("game:server.players.ban", permissions);
            Assert.Contains("game:server.players.mute", permissions);
            Assert.DoesNotContain(
                permissions,
                permission => permission.StartsWith("game:server.users.", StringComparison.Ordinal)
            );
        }
        finally
        {
            CommonLib.Net.Role = previous;
        }
    }

    [Fact]
    public void OfflineSubjectWithoutCharacterCanBeGrantedListedAndRevoked()
    {
        var previous = GameManager.Role;
        try
        {
            CommonLib.Net.Role = WorldRole.Server;
            using var project = new SubjectProject();
            var address = Address("offline");
            var player = project.Players.Store.GetOrCreate(address);
            var registry = Registry();
            var dispatcher = new CommandDispatcher(registry);
            var context = new CommandContext(CommandInvocationChannel.Text, CommandPrincipal.LocalOperator, project);
            const string permission = "game:player.teleport.self";
            Assert.True(
                dispatcher.Execute(new GrantPlayerPermissionCommand(address.Value, permission, false), context).Success
            );
            Assert.Equal(Guid.Empty, player.CharacterId);
            Assert.Empty(project.Characters.Characters);
            var listed = dispatcher.Execute(new ListPlayerPermissionsCommand(address.Value), context);
            Assert.True(listed.Success);
            Assert.Contains(permission, listed.Message);
            Assert.True(
                dispatcher.Execute(new RevokePlayerPermissionCommand(address.Value, permission), context).Success
            );
            Assert.Empty(player.Permissions.Grants);
            var own = new CommandContext(CommandInvocationChannel.Text, CommandPrincipal.FromSubject(player), project);
            Assert.True(dispatcher.Execute(new ListOwnPermissionsCommand(), own).Success);
        }
        finally
        {
            CommonLib.Net.Role = previous;
        }
    }

    [Fact]
    public void CompletionUsesSubjectAddressesAndDoesNotCreateUnknownSubjects()
    {
        var previous = GameManager.Role;
        var previousProject = GameManager.Project;
        using var project = new SubjectProject();
        try
        {
            CommonLib.Net.Role = WorldRole.Server;
            typeof(GameManager).GetProperty(nameof(GameManager.Project))!.SetValue(null, project);
            var player = project.Players.Store.GetOrCreate(Address("known"));
            player.DisplayName = "An account without a character";
            var registry = Registry();
            var adapter = new TextCommandAdapter(registry);
            var suggestions = adapter.Suggest("/permission grant ", CommandPrincipal.LocalOperator);
            Assert.Equal(player.Address.Value, Assert.Single(suggestions).Value);
            var dispatcher = new CommandDispatcher(registry);
            var context = new CommandContext(CommandInvocationChannel.Text, CommandPrincipal.LocalOperator, project);
            foreach (var input in new[]
                         { "An account without a character", Guid.NewGuid().ToString(), Address("unknown").Value })
            {
                var result = dispatcher.Execute(new ListPlayerPermissionsCommand(input), context);
                Assert.False(result.Success);
                Assert.Equal("permission.player_not_found", result.Code);
            }

            Assert.Single(project.Players.Store.Entries);
        }
        finally
        {
            CommonLib.Net.Role = previous;
            typeof(GameManager).GetProperty(nameof(GameManager.Project))!.SetValue(null, previousProject);
        }
    }

    [Fact]
    public void BansUseSubjectAuthorizationAndPreserveCharacterAndPermissions()
    {
        var previous = GameManager.Role;
        var previousClients = CommonLib.Net.Clients;
        var pending = (Dictionary<Client, string>)typeof(NetNode)
            .GetField("_clientsToRemove", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(CommonLib.Net)!;
        using var project = new SubjectProject();
        var address = Address("banned");
        var player = project.Players.Store.GetOrCreate(address);
        var characterId = Guid.NewGuid();
        project.Players.Store.BindCharacter(address, characterId);
        var permission = new ResourceId(new ModId("game"), "player.teleport.self");
        player.Permissions.Grant(permission, false);
        var session = new Client(null, 1, Guid.NewGuid(), characterId, null) { PlayerAddress = address };
        var unrelated = new Client(null, 2, Guid.NewGuid(), characterId, null) { PlayerAddress = Address("other") };
        try
        {
            CommonLib.Net.Role = WorldRole.Server;
            CommonLib.Net.Clients = new Dictionary<int, Client> { [1] = session, [2] = unrelated };
            var dispatcher = new CommandDispatcher(Registry());
            var unauthorized = new CommandContext(
                CommandInvocationChannel.Network,
                CommandPrincipal.FromSubject(player),
                project
            );
            Assert.Equal(
                "command.forbidden",
                dispatcher.Execute(new BanPlayerCommand(address.Value, "rule"), unauthorized).Code
            );
            Assert.Null(player.Ban);
            var context = new CommandContext(CommandInvocationChannel.Console, CommandPrincipal.LocalOperator, project);
            foreach (var reason in new[] { " ", new string('x', 513), "first\nsecond" })
            {
                Assert.Equal(
                    "player.ban.invalid_reason",
                    dispatcher.Execute(new BanPlayerCommand(address.Value, reason), context).Code
                );
                Assert.Null(player.Ban);
            }

            Assert.True(dispatcher.Execute(new BanPlayerCommand(address.Value, "world rule"), context).Success);
            Assert.Equal("world rule", player.Ban!.Reason);
            Assert.Equal(
                "command.subject_banned",
                dispatcher.Execute(new ListOwnPermissionsCommand(), unauthorized).Code
            );
            Assert.Contains(session, pending.Keys);
            Assert.DoesNotContain(unrelated, pending.Keys);
            Assert.Contains(address.Value, dispatcher.Execute(new ListPlayerBansCommand(), context).Message);
            Assert.True(dispatcher.Execute(new UnbanPlayerCommand(address.Value), context).Success);
            Assert.Null(player.Ban);
            Assert.Equal(characterId, player.CharacterId);
            Assert.Contains(player.Permissions.Grants, grant => grant.Permission == permission);
            Assert.Equal("player.not_banned", dispatcher.Execute(new UnbanPlayerCommand(address.Value), context).Code);
        }
        finally
        {
            pending.Remove(session);
            pending.Remove(unrelated);
            CommonLib.Net.Clients = previousClients;
            CommonLib.Net.Role = previous;
        }
    }

    [Fact]
    public void MuteManagementSupportsOfflineCharacterlessSubjectsAndUsesItsOwnPermission()
    {
        var previous = GameManager.Role;
        try
        {
            CommonLib.Net.Role = WorldRole.Server;
            using var project = new SubjectProject();
            var player = project.Players.Store.GetOrCreate(Address("muted"));
            var dispatcher = new CommandDispatcher(Registry());
            var unauthorized = new CommandContext(
                CommandInvocationChannel.Network,
                CommandPrincipal.FromSubject(player),
                project
            );
            Assert.Equal(
                "command.forbidden",
                dispatcher.Execute(new SetPlayerMuteCommand(player.Address.Value, true), unauthorized).Code
            );
            var context = new CommandContext(CommandInvocationChannel.Console, CommandPrincipal.LocalOperator, project);
            Assert.True(dispatcher.Execute(new SetPlayerMuteCommand(player.Address.Value, true), context).Success);
            Assert.True(player.IsMuted);
            Assert.Equal(Guid.Empty, player.CharacterId);
            Assert.Contains(player.Address.Value, dispatcher.Execute(new ListMutedPlayersCommand(), context).Message);
            Assert.True(dispatcher.Execute(new SetPlayerMuteCommand(player.Address.Value, false), context).Success);
            Assert.False(player.IsMuted);
            Assert.DoesNotContain(
                player.Address.Value,
                dispatcher.Execute(new ListMutedPlayersCommand(), context).Message
            );
        }
        finally { CommonLib.Net.Role = previous; }
    }

    private static PlayerAddress Address(string text) => new(
        IdentityAddress.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(text)))
    );

    private static CommandRegistry Registry()
    {
        var registry = new CommandRegistry();
        BuiltInCommands.Register(registry, new ModId("game"));
        registry.Freeze();
        return registry;
    }

    private sealed class SubjectProject : Project
    {
        public SubsystemPlayers Players { get; } = new();

        public SubsystemCharacters Characters { get; } = new();

        public SubsystemPositionMarks Marks { get; } = new();

        public SubjectProject()
        {
            subsystems.Add(Players);
            subsystems.Add(Characters);
            subsystems.Add(Marks);
            WorldSubsystemTestSupport.Attach(this, Players);
            WorldSubsystemTestSupport.Attach(this, Characters);
            WorldSubsystemTestSupport.Attach(this, Marks);
        }
    }
}
