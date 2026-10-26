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

using Survivalcraft.Test.Subsystems;

namespace Survivalcraft.Test.Commands;

[Collection(NetworkWorkTypeCollection.Name)]
public sealed class SubjectPermissionCommandTest
{
    [Fact]
    public void OfflineSubjectWithoutCharacterCanBeGrantedListedAndRevoked()
    {
        var previous = CommonLib.WorkType;
        try
        {
            CommonLib.WorkType = WorkType.Server;
            using var project = new SubjectProject();
            var address = Address("offline");
            var user = project.Players.Store.GetOrCreate(address);
            var registry = Registry();
            var dispatcher = new CommandDispatcher(registry);
            var context = new CommandContext(CommandInvocationChannel.Text, CommandPrincipal.LocalOperator, project);
            const string permission = "game:player.teleport.self";
            Assert.True(
                dispatcher.Execute(new GrantUserPermissionCommand(address.Value, permission, false), context).Success
            );
            Assert.Equal(Guid.Empty, user.CharacterId);
            Assert.Empty(project.Characters.Characters);
            var listed = dispatcher.Execute(new ListUserPermissionsCommand(address.Value), context);
            Assert.True(listed.Success);
            Assert.Contains(permission, listed.Message);
            Assert.True(
                dispatcher.Execute(new RevokeUserPermissionCommand(address.Value, permission), context).Success
            );
            Assert.Empty(user.Permissions.Grants);
            var own = new CommandContext(CommandInvocationChannel.Text, CommandPrincipal.FromSubject(user), project);
            Assert.True(dispatcher.Execute(new ListOwnPermissionsCommand(), own).Success);
        }
        finally
        {
            CommonLib.WorkType = previous;
        }
    }

    [Fact]
    public void CompletionUsesSubjectAddressesAndDoesNotCreateUnknownSubjects()
    {
        var previous = CommonLib.WorkType;
        var previousProject = GameManager.Project;
        using var project = new SubjectProject();
        try
        {
            CommonLib.WorkType = WorkType.Server;
            typeof(GameManager).GetProperty(nameof(GameManager.Project))!.SetValue(null, project);
            var user = project.Players.Store.GetOrCreate(Address("known"));
            user.DisplayName = "An account without a character";
            var registry = Registry();
            var adapter = new TextCommandAdapter(registry);
            var suggestions = adapter.Suggest("/permission grant ", CommandPrincipal.LocalOperator);
            Assert.Equal(user.Address.Value, Assert.Single(suggestions).Value);
            var dispatcher = new CommandDispatcher(registry);
            var context = new CommandContext(CommandInvocationChannel.Text, CommandPrincipal.LocalOperator, project);
            foreach (var input in new[]
                         { "An account without a character", Guid.NewGuid().ToString(), Address("unknown").Value })
            {
                var result = dispatcher.Execute(new ListUserPermissionsCommand(input), context);
                Assert.False(result.Success);
                Assert.Equal("permission.user_not_found", result.Code);
            }

            Assert.Single(project.Players.Store.Entries);
        }
        finally
        {
            CommonLib.WorkType = previous;
            typeof(GameManager).GetProperty(nameof(GameManager.Project))!.SetValue(null, previousProject);
        }
    }

    [Fact]
    public void BansUseSubjectAuthorizationAndPreserveCharacterAndPermissions()
    {
        var previous = CommonLib.WorkType;
        var previousClients = CommonLib.Net.Clients;
        var pending = (Dictionary<Client, string>)typeof(NetNode)
            .GetField("_clientsToRemove", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(CommonLib.Net)!;
        using var project = new SubjectProject();
        var address = Address("banned");
        var user = project.Players.Store.GetOrCreate(address);
        var characterId = Guid.NewGuid();
        project.Players.Store.BindCharacter(address, characterId);
        var permission = new ResourceId(new ModId("game"), "player.teleport.self");
        user.Permissions.Grant(permission, false);
        var session = new Client(null, 1, Guid.NewGuid(), characterId, null) { PlayerAddress = address };
        var unrelated = new Client(null, 2, Guid.NewGuid(), characterId, null) { PlayerAddress = Address("other") };
        try
        {
            CommonLib.WorkType = WorkType.Server;
            CommonLib.Net.Clients = new Dictionary<int, Client> { [1] = session, [2] = unrelated };
            var dispatcher = new CommandDispatcher(Registry());
            var unauthorized = new CommandContext(
                CommandInvocationChannel.Network,
                CommandPrincipal.FromSubject(user),
                project
            );
            Assert.Equal(
                "command.forbidden",
                dispatcher.Execute(new BanUserCommand(address.Value, "rule"), unauthorized).Code
            );
            Assert.Null(user.Ban);
            var context = new CommandContext(CommandInvocationChannel.Console, CommandPrincipal.LocalOperator, project);
            foreach (var reason in new[] { " ", new string('x', 513), "first\nsecond" })
            {
                Assert.Equal(
                    "user.ban.invalid_reason",
                    dispatcher.Execute(new BanUserCommand(address.Value, reason), context).Code
                );
                Assert.Null(user.Ban);
            }

            Assert.True(dispatcher.Execute(new BanUserCommand(address.Value, "world rule"), context).Success);
            Assert.Equal("world rule", user.Ban!.Reason);
            Assert.Equal(
                "command.subject_banned",
                dispatcher.Execute(new ListOwnPermissionsCommand(), unauthorized).Code
            );
            Assert.Contains(session, pending.Keys);
            Assert.DoesNotContain(unrelated, pending.Keys);
            Assert.Contains(address.Value, dispatcher.Execute(new ListPlayerBansCommand(), context).Message);
            Assert.True(dispatcher.Execute(new UnbanUserCommand(address.Value), context).Success);
            Assert.Null(user.Ban);
            Assert.Equal(characterId, user.CharacterId);
            Assert.Contains(user.Permissions.Grants, grant => grant.Permission == permission);
            Assert.Equal("user.not_banned", dispatcher.Execute(new UnbanUserCommand(address.Value), context).Code);
        }
        finally
        {
            pending.Remove(session);
            pending.Remove(unrelated);
            CommonLib.Net.Clients = previousClients;
            CommonLib.WorkType = previous;
        }
    }

    [Fact]
    public void MuteManagementSupportsOfflineCharacterlessSubjectsAndUsesItsOwnPermission()
    {
        var previous = CommonLib.WorkType;
        try
        {
            CommonLib.WorkType = WorkType.Server;
            using var project = new SubjectProject();
            var user = project.Players.Store.GetOrCreate(Address("muted"));
            var dispatcher = new CommandDispatcher(Registry());
            var unauthorized = new CommandContext(
                CommandInvocationChannel.Network,
                CommandPrincipal.FromSubject(user),
                project
            );
            Assert.Equal(
                "command.forbidden",
                dispatcher.Execute(new SetUserMuteCommand(user.Address.Value, true), unauthorized).Code
            );
            var context = new CommandContext(CommandInvocationChannel.Console, CommandPrincipal.LocalOperator, project);
            Assert.True(dispatcher.Execute(new SetUserMuteCommand(user.Address.Value, true), context).Success);
            Assert.True(user.IsMuted);
            Assert.Equal(Guid.Empty, user.CharacterId);
            Assert.Contains(user.Address.Value, dispatcher.Execute(new ListMutedUsersCommand(), context).Message);
            Assert.True(dispatcher.Execute(new SetUserMuteCommand(user.Address.Value, false), context).Success);
            Assert.False(user.IsMuted);
            Assert.DoesNotContain(user.Address.Value, dispatcher.Execute(new ListMutedUsersCommand(), context).Message);
        }
        finally { CommonLib.WorkType = previous; }
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
