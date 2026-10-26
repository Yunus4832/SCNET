using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;
using Engine.Core.Runtime;
using Engine.Core.Security;

using EntitySystem.Core;

using Game.Characters;
using Game.Commands;
using Game.Components;
using Game.Network;
using Game.Protocol.Identity;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Subsystems;

namespace Survivalcraft.Test.Commands;

[Collection(NetworkWorkTypeCollection.Name)]
public class TeleportCompletionTest : IDisposable
{
    private readonly Dictionary<int, Client> _previousClients = CommonLib.Net.Clients;

    public TeleportCompletionTest()
    {
        CommonLib.Net.Clients = [];
    }

    public void Dispose()
    {
        CommonLib.Net.Clients = _previousClients;
    }

    [Fact]
    public void MarkNamesAreScopedAndAvailableForTeleportAndOverwrite()
    {
        var project = new CompletionProject();
        var player = CreatePlayer(project, "Home One");
        var other = CreatePlayer(project, "Secret");
        project.Marks.PublicMarks.Set("Shared", Vector3.Zero);
        var permissions = new[]
        {
            new ResourceId(new ModId("game"), "player.teleport.self"),
            new ResourceId(new ModId("game"), "world.mark.manage")
        };
        var principal = Principal(player, permissions);
        var adapter = new TextCommandAdapter(Registry());
        foreach (var command in new[] { "/tp self", "/mark" })
        {
            Assert.Equal("Home One", Assert.Single(adapter.Suggest(command + " private ", principal)).Value);
            Assert.Equal("Shared", Assert.Single(adapter.Suggest(command + " public ", principal)).Value);
            Assert.DoesNotContain(
                adapter.Suggest(command + " private ", principal),
                suggestion => suggestion.Value == "Secret"
            );
        }

        var otherPrincipal = Principal(other, permissions);
        Assert.Equal("Secret", Assert.Single(adapter.Suggest("/tp self private ", otherPrincipal)).Value);
    }

    [Fact]
    public void SpawnMarksUseOrdinaryMarkCompletionAndCommands()
    {
        var adapter = new TextCommandAdapter(Registry());
        var project = new CompletionProject();
        var player = CreatePlayer(project, "spawn");
        project.Marks.PublicMarks.Set("spawn", new Vector3(10f, 65f, 10f));
        var principal = Principal(player, [new ResourceId(new ModId("game"), "player.teleport.self")]);
        Assert.Equal("self", Assert.Single(adapter.Suggest("/tp ", principal)).Value);
        var suggestions = adapter.Suggest("/tp self ", principal).Select(suggestion => suggestion.Value).ToArray();
        Assert.DoesNotContain("spawn", suggestions);
        Assert.DoesNotContain("worldspawn", suggestions);
        Assert.Contains("private", suggestions);
        Assert.Contains("public", suggestions);
        Assert.Contains("position", suggestions);
        Assert.Contains("player", suggestions);
        Assert.Equal("spawn", Assert.Single(adapter.Suggest("/tp self private ", principal)).Value);
        Assert.Equal("spawn", Assert.Single(adapter.Suggest("/tp self public ", principal)).Value);
        Assert.True(adapter.TryFind("tp", out var entry));
        foreach (var world in new[] { false, true })
        {
            var literal = world ? "public" : "private";
            var route = Assert.Single(
                entry!.Command.Routes,
                route => route.Segments.Count == 3 &&
                         route.Segments[0] is CommandLiteral
                         {
                             Value: "self"
                         } &&
                         route.Segments[1] is CommandLiteral token &&
                         token.Value == literal
            );
            Assert.Equal(
                new TeleportMarkCommand("spawn", world),
                route.CreateCommand(new CommandArguments(new Dictionary<string, object> { ["name"] = "spawn" }))
            );
            var registry = Registry();
            Assert.True(
                registry.TryEncode(
                    new TeleportMarkCommand("spawn", world),
                    out var id,
                    out var payload,
                    out _
                )
            );
            Assert.True(registry.TryDecode(id, payload, out var restored, out _));
            Assert.Equal(new TeleportMarkCommand("spawn", world), restored);
        }
    }

    [Fact]
    public void PlayerRoutesCompleteBothPlayerArgumentsAndRespectOtherPlayerPermission()
    {
        var project = new CompletionProject();
        var player = CreatePlayer(project, "Player One");
        var other = CreatePlayer(project, "Other");
        player.Name = "Player One";
        other.Name = "Other";
        var players =
            (List<CharacterData>)typeof(SubsystemCharacters).GetField(
                "_characters",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.GetValue(project.Characters)!;
        players.AddRange([player, other]);
        var selfPermission = new ResourceId(new ModId("game"), "player.teleport.self");
        var othersPermission = new ResourceId(new ModId("game"), "player.teleport.others");
        var principal =
            Principal(player, [selfPermission, othersPermission]);
        var adapter = new TextCommandAdapter(Registry());
        Assert.Equal(new[] { "player", "self" }, adapter.Suggest("/tp ", principal).Select(item => item.Value));
        Assert.Equal(
            "TeleportSelf_Description",
            Assert.Single(adapter.Suggest("/tp ", principal), item => item.Value == "self").DescriptionSource!.Key
        );
        Assert.Equal(
            "TeleportOther_Description",
            Assert.Single(adapter.Suggest("/tp ", principal), item => item.Value == "player").DescriptionSource!.Key
        );
        Assert.Equal(
            new[] { "Other", "Player One" },
            adapter.Suggest("/tp player ", principal).Select(item => item.Value)
        );
        Assert.Equal(
            new[] { "player", "position", "private", "public" },
            adapter.Suggest("/tp player \"Player One\" ", principal).Select(item => item.Value)
        );
        project.Marks.PublicMarks.Set("spawn", new Vector3(10f, 65f, 10f));
        Assert.Equal("Other", Assert.Single(adapter.Suggest("/tp player Other private ", principal)).Value);
        Assert.Equal(
            "Player One",
            Assert.Single(adapter.Suggest("/tp player \"Player One\" private ", principal)).Value
        );
        Assert.Equal(
            "Other",
            Assert.Single(adapter.Suggest($"/tp player {other.CharacterId} private ", principal)).Value
        );
        Assert.Equal("spawn", Assert.Single(adapter.Suggest("/tp player Other public ", principal)).Value);
        Assert.Equal("Other", Assert.Single(adapter.Suggest("/tp player \"Player One\" player ", principal)).Value);
        Assert.Equal("Player One", Assert.Single(adapter.Suggest("/tp PLAYER OTHER player ", principal)).Value);
        Assert.Equal("Other", Assert.Single(adapter.Suggest("/tp self player ", principal)).Value);
        Assert.Equal(
            "Player One",
            Assert.Single(adapter.Suggest($"/tp player {other.CharacterId} player ", principal)).Value
        );
        var selfOnly = Principal(player, [selfPermission]);
        Assert.Equal("self", Assert.Single(adapter.Suggest("/tp ", selfOnly)).Value);
        Assert.Empty(adapter.Suggest("/tp player ", selfOnly));
        Assert.Empty(adapter.Suggest("/tp player Other private ", selfOnly));
        Assert.Empty(adapter.Suggest("/tp player Other public ", selfOnly));
        Assert.Equal("Other", Assert.Single(adapter.Suggest("/tp self player ", selfOnly)).Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OtherPlayerMarkLookupUsesTheTargetScopeNotTheCaller(bool isPublic)
    {
        var project = new CompletionProject();
        var caller = CreatePlayer(project, "CallerOnly");
        var target = CreatePlayer(project, "TargetOnly");
        target.Name = "Target";
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(target, true);
        var stateMachine = new StateMachine();
        stateMachine.AddState("Playing", () => { }, () => { }, () => { });
        stateMachine.TransitionTo("Playing");
        typeof(CharacterData).GetField("_stateMachine", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(
            target,
            stateMachine
        );
        var players =
            (List<CharacterData>)typeof(SubsystemCharacters).GetField(
                "_characters",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.GetValue(project.Characters)!;
        players.Add(target);
        target.ComponentCharacter = new ComponentCharacter
        {
            CharacterData = target,
            ComponentHealth = new ComponentHealth { Health = 1f },
            ComponentBody = new ComponentBody()
        };
        var context = new CommandContext(
            CommandInvocationChannel.Text,
            Principal(caller, []),
            project
        );
        Assert.Equal(
            "mark.missing",
            TeleportCommandHandlers.PlayerMarkTeleport(
                context,
                new TeleportPlayerMarkCommand("Target", "CallerOnly", isPublic)
            ).Code
        );
        var marks = isPublic ? project.Marks.PublicMarks : OwnedFixtureMarks(target);
        marks.Set("spawn", new Vector3(float.NaN, 65f, 0f));
        // An invalid coordinate proves the target/shared mark was resolved before terrain or movement is touched.
        Assert.Equal(
            "teleport.invalid_position",
            TeleportCommandHandlers.PlayerMarkTeleport(
                context,
                new TeleportPlayerMarkCommand(target.CharacterId.ToString(), "SPAWN", isPublic)
            ).Code
        );
        Assert.Null(target.PendingTeleport);
        Assert.False(OwnedFixtureMarks(target).TryGet("previous", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TeleportingToTheSamePlayerDoesNotMoveOrOverwritePrevious(bool useGuid)
    {
        var project = new CompletionProject();
        var player = CreatePlayer(project, "Tester");
        player.Name = "Tester";
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(player, true);
        var stateMachine = new StateMachine();
        stateMachine.AddState("Playing", () => { }, () => { }, () => { });
        stateMachine.TransitionTo("Playing");
        typeof(CharacterData).GetField("_stateMachine", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(
            player,
            stateMachine
        );
        var players =
            (List<CharacterData>)typeof(SubsystemCharacters).GetField(
                "_characters",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.GetValue(project.Characters)!;
        players.Add(player);
        var body = new ComponentBody { Position = new Vector3(20f, 65f, 20f), Velocity = Vector3.UnitX };
        player.ComponentCharacter = new ComponentCharacter
        {
            CharacterData = player,
            ComponentHealth = new ComponentHealth { Health = 1f },
            ComponentBody = body
        };
        var previous = new Vector3(100f, 65f, 100f);
        OwnedFixtureMarks(player).Set("previous", previous);
        var context = new CommandContext(
            CommandInvocationChannel.Text,
            Principal(player, []),
            project
        );
        var destination = useGuid ? player.CharacterId.ToString() : "TESTER";
        var result = TeleportCommandHandlers.Self(context, new TeleportSelfCommand(DestinationPlayer: destination));
        Assert.Equal("teleport.unchanged", result.Code);
        Assert.True(result.Success);
        Assert.Equal(new Vector3(20f, 65f, 20f), body.Position);
        Assert.Equal(Vector3.UnitX, body.Velocity);
        Assert.True(OwnedFixtureMarks(player).TryGet("previous", out var saved));
        Assert.Equal(previous, saved);
        Assert.Null(player.PendingTeleport);
    }

    [Fact]
    public void MarkCommandsAndCompletionUseTheActorAndTargetControllerInsteadOfRoleOwners()
    {
        var project = new CompletionProject();
        var actorRole = CreatePlayer(project, "ActorSecret");
        var target = CreatePlayer(project, "OwnerSecret");
        var caller = CreatePlayer(project, "CallerSecret");
        target.Name = "Target";
        var targetSession = CommonLib.Net.Clients.Values.Single(session => session.CharacterId == target.CharacterId);
        CommonLib.Net.Clients.Remove(targetSession.ID);
        var actorSession = CommonLib.Net.Clients.Values.Single(session => session.CharacterId == actorRole.CharacterId);
        actorSession.CharacterId = target.CharacterId;
        var records =
            (List<CharacterData>)typeof(SubsystemCharacters).GetField(
                "_characters",
                BindingFlags.NonPublic | BindingFlags.Instance
            )!.GetValue(project.Characters)!;
        records.AddRange([target, caller]);
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(target, true);
        var machine = new StateMachine();
        machine.AddState("Playing", () => { }, () => { }, () => { });
        machine.TransitionTo("Playing");
        typeof(CharacterData).GetField("_stateMachine", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(
            target,
            machine
        );
        target.ComponentCharacter = new ComponentCharacter
        {
            CharacterData = target, ComponentHealth = new ComponentHealth { Health = 1f },
            ComponentBody = new ComponentBody { Position = new Vector3(9f, 65f, 9f) }
        };
        var actor = actorRole.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.GetOrCreate(actorRole.OwnerAddress);
        actor.Permissions.Grant(ResourceId.Parse("game:player.teleport.self"), false);
        var principal = CommandPrincipal.FromSubject(actor, target);
        var context = new CommandContext(CommandInvocationChannel.Text, principal, project);
        Assert.True(
            TeleportCommandHandlers.MarkPrivate(context, new MarkPrivatePositionCommand("actor-owned")).Success
        );
        Assert.True(project.Marks.GetPrivateMarks(actor.Address).TryGet("actor-owned", out _));
        Assert.False(OwnedFixtureMarks(target).TryGet("actor-owned", out _));
        var adapter = new TextCommandAdapter(Registry());
        Assert.Contains(adapter.Suggest("/tp self private ", principal), item => item.Value == "ActorSecret");
        Assert.DoesNotContain(adapter.Suggest("/tp self private ", principal), item => item.Value == "OwnerSecret");
        var otherPrincipal = Principal(caller, [ResourceId.Parse("game:player.teleport.others")]);
        Assert.Contains(
            adapter.Suggest("/tp player Target private ", otherPrincipal),
            item => item.Value == "ActorSecret"
        );
        Assert.DoesNotContain(
            adapter.Suggest("/tp player Target private ", otherPrincipal),
            item => item.Value == "OwnerSecret"
        );
        project.Marks.GetPrivateMarks(actor.Address).Set("ActorSecret", new Vector3(float.NaN, 65f, 0f));
        var otherContext = new CommandContext(CommandInvocationChannel.Text, otherPrincipal, project);
        Assert.Equal(
            "teleport.invalid_position",
            TeleportCommandHandlers.PlayerMarkTeleport(
                otherContext,
                new TeleportPlayerMarkCommand("Target", "ActorSecret", false)
            ).Code
        );
    }

    private static PositionMarks OwnedFixtureMarks(CharacterData player)
    {
        return player.Project.FindSubsystem<SubsystemPositionMarks>(true)!.GetPrivateMarks(player.OwnerAddress);
    }

    private static CommandPrincipal Principal(CharacterData player, IEnumerable<ResourceId> permissions)
    {
        var subject = player.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.GetOrCreate(player.OwnerAddress);
        foreach (var grant in subject.Permissions.Grants.ToArray()) { subject.Permissions.Revoke(grant.Permission); }

        foreach (var permission in permissions) { subject.Permissions.Grant(permission, false); }

        return CommandPrincipal.FromSubject(subject, player);
    }

    private static CommandRegistry Registry()
    {
        var registry = new CommandRegistry();
        BuiltInCommands.Register(registry, new ModId("game"));
        registry.Freeze();
        return registry;
    }

    private static CharacterData CreatePlayer(Project project, string name)
    {
        // Explicit subject/controller fixtures; no live transport, running character or graphics.
        var player = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        player.CharacterId = Guid.NewGuid();
        var digest = System.Security.Cryptography.SHA256.HashData(player.CharacterId.ToByteArray());
        player.OwnerAddress =
            new PlayerAddress(IdentityAddress.Encode(digest));
        typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(player, project);
        player.SubsystemCharacters = project.FindSubsystem<SubsystemCharacters>(true)!;
        OwnedFixtureMarks(player).Set(name, Vector3.Zero);
        var id = (byte)(CommonLib.Net.Clients.Count + 1);
        CommonLib.Net.Clients[id] = new Client(null, id, Guid.NewGuid(), player.CharacterId, project)
            { PlayerAddress = player.OwnerAddress };
        return player;
    }

    private sealed class CompletionProject : Project
    {
        public SubsystemPlayers Players { get; } = new();

        public SubsystemCharacters Characters { get; } = new();

        public SubsystemPositionMarks Marks { get; } = new();

        public CompletionProject()
        {
            subsystems.Add(Players);
            subsystems.Add(Characters);
            subsystems.Add(Marks);
            WorldSubsystemTestSupport.Attach(this, Players);
            WorldSubsystemTestSupport.Attach(this, Characters);
            WorldSubsystemTestSupport.Attach(this, Marks);
            subsystems.Add(
                new SubsystemGameInfo
                    { WorldSettings = new WorldSettings { GameMode = GameMode.Survival } }
            );
        }
    }
}
