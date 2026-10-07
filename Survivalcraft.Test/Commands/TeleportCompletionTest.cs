using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core;

using EntitySystem.Core;

using Game;
using Game.Commands;
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
        foreach (var command in new[] { "/tp", "/mark" })
        {
            Assert.Equal("Home One", Assert.Single(adapter.Suggest(command + " private ", principal)).Value);
            Assert.Equal("Shared", Assert.Single(adapter.Suggest(command + " public ", principal)).Value);
            Assert.DoesNotContain(adapter.Suggest(command + " private ", principal), suggestion => suggestion.Value == "Secret");
        }

        var otherPrincipal = new CommandPrincipal("Other", player: other, permissions: permissions);
        Assert.Equal("Secret", Assert.Single(adapter.Suggest("/tp private ", otherPrincipal)).Value);
    }

    [Fact]
    public void SpawnRoutesHaveCompletionAndUseTypedSpawnCommands()
    {
        var adapter = new TextCommandAdapter(Registry());
        var principal = new CommandPrincipal("Tester", permissions:
            [new ResourceId(new ModId("game"), "player.teleport.self")]);
        var suggestions = adapter.Suggest("/tp ", principal).Select(suggestion => suggestion.Value).ToArray();
        Assert.Contains("spawn", suggestions);
        Assert.Contains("worldspawn", suggestions);
        Assert.Contains("private", suggestions);
        Assert.Contains("public", suggestions);
        Assert.Empty(adapter.Suggest("/tp spawn ", principal));
        Assert.Empty(adapter.Suggest("/tp worldspawn ", principal));
        Assert.True(adapter.TryFind("tp", out var entry));
        foreach (var world in new[] { false, true })
        {
            var literal = world ? "worldspawn" : "spawn";
            var route = Assert.Single(entry!.Command.Routes, route => route.Segments.Count == 1 &&
                route.Segments[0] is CommandLiteral token && token.Value == literal);
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
        Assert.Equal(new[] { "Other", "Player One" }, adapter.Suggest("/tp player ", principal).Select(item => item.Value));
        Assert.Contains(adapter.Suggest("/tp player \"Player One\" ", principal), item => item.Value == "Other");
        var selfOnly = new CommandPrincipal("Tester", player: player, permissions: [selfPermission]);
        Assert.DoesNotContain(adapter.Suggest("/tp player \"Player One\" ", selfOnly), item => item.Value == "Other");
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
