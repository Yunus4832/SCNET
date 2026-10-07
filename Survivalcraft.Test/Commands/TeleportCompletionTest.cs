using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core;

using EntitySystem.Core;

using Game;
using Game.Commands;
using Game.Components;
using Game.Modding;
using Game.Subsystems;

namespace Survivalcraft.Test.Commands;

public class TeleportCompletionTest
{
    [Fact]
    public void MarkNamesAreScopedAndAvailableForTeleportAndOverwrite()
    {
        var project = new CompletionProject();
        var player = CreatePlayer(project, "Home One");
        var other = CreatePlayer(project, "Secret");
        project.Players.PublicMarks.Set("Shared", Vector3.Zero);
        var permissions = new[]
        {
            new ResourceId(new ModId("game"), "player.teleport.self"),
            new ResourceId(new ModId("game"), "world.mark.manage")
        };
        var principal = new CommandPrincipal("Tester", player: player, permissions: permissions);
        var adapter = new TextCommandAdapter(Registry());
        foreach (var command in new[] { "/tp self", "/mark" })
        {
            Assert.Equal("Home One", Assert.Single(adapter.Suggest(command + " private ", principal)).Value);
            Assert.Equal("Shared", Assert.Single(adapter.Suggest(command + " public ", principal)).Value);
            Assert.DoesNotContain(adapter.Suggest(command + " private ", principal), suggestion => suggestion.Value == "Secret");
        }

        var otherPrincipal = new CommandPrincipal("Other", player: other, permissions: permissions);
        Assert.Equal("Secret", Assert.Single(adapter.Suggest("/tp self private ", otherPrincipal)).Value);
    }

    [Fact]
    public void SpawnRoutesHaveCompletionAndUseTypedSpawnCommands()
    {
        var adapter = new TextCommandAdapter(Registry());
        var principal = new CommandPrincipal("Tester", permissions:
            [new ResourceId(new ModId("game"), "player.teleport.self")]);
        Assert.Equal("self", Assert.Single(adapter.Suggest("/tp ", principal)).Value);
        var suggestions = adapter.Suggest("/tp self ", principal).Select(suggestion => suggestion.Value).ToArray();
        Assert.Contains("spawn", suggestions);
        Assert.Contains("worldspawn", suggestions);
        Assert.Contains("private", suggestions);
        Assert.Contains("public", suggestions);
        Assert.Contains("position", suggestions);
        Assert.Contains("player", suggestions);
        Assert.Empty(adapter.Suggest("/tp self spawn ", principal));
        Assert.Empty(adapter.Suggest("/tp self worldspawn ", principal));
        Assert.True(adapter.TryFind("tp", out var entry));
        foreach (var world in new[] { false, true })
        {
            var literal = world ? "worldspawn" : "spawn";
            var route = Assert.Single(entry!.Command.Routes, route => route.Segments.Count == 2 &&
                route.Segments[0] is CommandLiteral { Value: "self" } &&
                route.Segments[1] is CommandLiteral token && token.Value == literal);
            Assert.Equal(new TeleportSpawnCommand(world), route.CreateCommand(new CommandArguments([])));
            var registry = Registry();
            Assert.True(registry.TryEncode(new TeleportSpawnCommand(world), out var id, out var payload, out _));
            Assert.True(registry.TryDecode(id, payload, out var restored, out _));
            Assert.Equal(new TeleportSpawnCommand(world), restored);
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
        var players = (List<PlayerData>)typeof(SubsystemPlayers).GetField("_playersData", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(project.Players)!;
        players.AddRange([player, other]);
        var selfPermission = new ResourceId(new ModId("game"), "player.teleport.self");
        var othersPermission = new ResourceId(new ModId("game"), "player.teleport.others");
        var principal = new CommandPrincipal("Tester", player: player, permissions: [selfPermission, othersPermission]);
        var adapter = new TextCommandAdapter(Registry());
        Assert.Equal(new[] { "player", "self" }, adapter.Suggest("/tp ", principal).Select(item => item.Value));
        Assert.Equal("TeleportSelf_Description", Assert.Single(adapter.Suggest("/tp ", principal), item => item.Value == "self").DescriptionSource!.Key);
        Assert.Equal("TeleportOther_Description", Assert.Single(adapter.Suggest("/tp ", principal), item => item.Value == "player").DescriptionSource!.Key);
        Assert.Equal(new[] { "Other", "Player One" }, adapter.Suggest("/tp player ", principal).Select(item => item.Value));
        Assert.Equal(new[] { "player", "position" }, adapter.Suggest("/tp player \"Player One\" ", principal).Select(item => item.Value));
        Assert.Equal("Other", Assert.Single(adapter.Suggest("/tp player \"Player One\" player ", principal)).Value);
        Assert.Equal("Player One", Assert.Single(adapter.Suggest("/tp PLAYER OTHER player ", principal)).Value);
        Assert.Equal("Other", Assert.Single(adapter.Suggest("/tp self player ", principal)).Value);
        Assert.Equal("Player One", Assert.Single(adapter.Suggest($"/tp player {other.PlayerGUID} player ", principal)).Value);
        var selfOnly = new CommandPrincipal("Tester", player: player, permissions: [selfPermission]);
        Assert.Equal("self", Assert.Single(adapter.Suggest("/tp ", selfOnly)).Value);
        Assert.Empty(adapter.Suggest("/tp player ", selfOnly));
        Assert.Equal("Other", Assert.Single(adapter.Suggest("/tp self player ", selfOnly)).Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TeleportingToTheSamePlayerDoesNotMoveOrOverwritePrevious(bool useGuid)
    {
        var project = new CompletionProject();
        var player = CreatePlayer(project, "Tester");
        player.Name = "Tester";
        player.SetMain();
        var stateMachine = new StateMachine();
        stateMachine.AddState("Playing", () => { }, () => { }, () => { });
        stateMachine.TransitionTo("Playing");
        typeof(PlayerData).GetField("_stateMachine", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, stateMachine);
        var players = (List<PlayerData>)typeof(SubsystemPlayers).GetField("_playersData", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(project.Players)!;
        players.Add(player);
        var body = new ComponentBody { Position = new Vector3(20f, 65f, 20f), Velocity = Vector3.UnitX };
        player.ComponentPlayer = new ComponentPlayer
        {
            PlayerData = player,
            ComponentHealth = new ComponentHealth { Health = 1f },
            ComponentBody = body
        };
        var previous = new Vector3(100f, 65f, 100f);
        player.PrivateMarks.Set("previous", previous);
        var context = new CommandContext(CommandInvocationChannel.Text, new CommandPrincipal("Tester", player: player), project);
        var destination = useGuid ? player.PlayerGUID.ToString() : "TESTER";
        var result = TeleportCommandHandlers.Self(context, new TeleportSelfCommand(DestinationPlayer: destination));
        Assert.Equal("teleport.unchanged", result.Code);
        Assert.True(result.Success);
        Assert.Equal(new Vector3(20f, 65f, 20f), body.Position);
        Assert.Equal(Vector3.UnitX, body.Velocity);
        Assert.True(player.PrivateMarks.TryGet("previous", out var saved));
        Assert.Equal(previous, saved);
        Assert.Null(player.PendingTeleport);
    }

    private static CommandRegistry Registry()
    {
        var registry = new CommandRegistry();
        BuiltInCommands.Register(registry, new ModId("game"));
        registry.Freeze();
        return registry;
    }

    private static PlayerData CreatePlayer(Project project, string name)
    {
        // Completion only needs ownership and mark names, not a running character or graphics.
        var player = (PlayerData)RuntimeHelpers.GetUninitializedObject(typeof(PlayerData));
        player.PlayerGUID = Guid.NewGuid();
        typeof(PlayerData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, project);
        var marks = new PositionMarks();
        marks.Set(name, Vector3.Zero);
        typeof(PlayerData).GetField("<PrivateMarks>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, marks);
        return player;
    }

    private sealed class CompletionProject : Project
    {
        public SubsystemPlayers Players { get; } = new();

        public CompletionProject()
        {
            subsystems.Add(Players);
            subsystems.Add(new SubsystemGameInfo { WorldSettings = new WorldSettings { GameMode = GameMode.Survival } });
        }
    }
}
