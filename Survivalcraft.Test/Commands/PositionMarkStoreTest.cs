using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game;
using Game.Subsystems;

namespace Survivalcraft.Test.Commands;

public class PositionMarkStoreTest
{
    [Fact]
    public void WorldMarksRoundTripWithOfflinePlayersAndIndependentScopes()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var store = new PositionMarkStore();
        store.PublicMarks.Set("home", new Vector3(1f));
        store.GetPrivateMarks(first).Set("home", new Vector3(2f));
        store.GetPrivateMarks(second).Set("home", new Vector3(3f));
        var world = new ProjectExtensionData();
        world.Get("game", "position-marks").ApplyOverrides(store.Save());

        var restoredWorld = new ProjectExtensionData();
        restoredWorld.Load(world.Save());
        var restored = new PositionMarkStore();
        restored.Load(restoredWorld.Get("game", "position-marks"));
        Assert.True(restored.PublicMarks.TryGet("home", out var shared));
        Assert.Equal(new Vector3(1f), shared);
        Assert.True(restored.GetPrivateMarks(first).TryGet("home", out var personal));
        Assert.Equal(new Vector3(2f), personal);
        Assert.True(restored.GetPrivateMarks(second).TryGet("home", out personal));
        Assert.Equal(new Vector3(3f), personal);
        Assert.False(restored.GetPrivateMarks(Guid.NewGuid()).TryGet("home", out _));
        restored.Load(new ValuesDictionary());
        Assert.Empty(restored.PublicMarks.Names);
        Assert.Empty(restored.GetPrivateMarks(first).Names);
    }

    [Fact]
    public void RecreatedPlayerDataUsesTheSameWorldMarksWithoutSharingWithOtherPlayers()
    {
        var players = new SubsystemPlayers();
        var guid = Guid.NewGuid();
        var original = CreatePlayer(players, guid);
        original.PrivateMarks.Set("previous", new Vector3(4f));
        var recreated = CreatePlayer(players, guid);
        Assert.Same(original.PrivateMarks, recreated.PrivateMarks);
        Assert.True(recreated.PrivateMarks.TryGet("previous", out var previous));
        Assert.Equal(new Vector3(4f), previous);
        Assert.Empty(CreatePlayer(players, Guid.NewGuid()).PrivateMarks.Names);
        Assert.Empty(CreatePlayer(new SubsystemPlayers(), guid).PrivateMarks.Names);
    }

    [Fact]
    public void PlayerSubsystemStoresMarksOnlyInWorldDataAndClientSaveDoesNotOverwriteThem()
    {
        using var project = new Project();
        var players = new SubsystemPlayers();
        typeof(Subsystem).GetProperty(nameof(Subsystem.Project))!.SetValue(players, project);
        typeof(Subsystem).GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(players, true);
        var guid = Guid.NewGuid();
        players.PublicMarks.Set("shared", new Vector3(1f));
        players.GetPrivateMarks(guid).Set("personal", new Vector3(2f));
        var subsystemData = new ValuesDictionary();
        players.Save(subsystemData);
        Assert.False(subsystemData.ContainsKey("PublicMarks"));
        var restored = new PositionMarkStore();
        restored.Load(project.ExtensionData.Get("game", "position-marks"));
        Assert.True(restored.GetPrivateMarks(guid).TryGet("personal", out var point));
        Assert.Equal(new Vector3(2f), point);

        players.GetPrivateMarks(guid).Set("personal", new Vector3(3f));
        project.SendToClientMode = true;
        players.Save(new ValuesDictionary());
        restored.Load(project.ExtensionData.Get("game", "position-marks"));
        Assert.True(restored.GetPrivateMarks(guid).TryGet("personal", out point));
        Assert.Equal(new Vector3(2f), point);
        project.SendToClientMode = false;
        players.Save(new ValuesDictionary());
        restored.Load(project.ExtensionData.Get("game", "position-marks"));
        Assert.True(restored.GetPrivateMarks(guid).TryGet("personal", out point));
        Assert.Equal(new Vector3(3f), point);
    }

    private static PlayerData CreatePlayer(SubsystemPlayers players, Guid guid)
    {
        var player = (PlayerData)RuntimeHelpers.GetUninitializedObject(typeof(PlayerData));
        player.SubsystemPlayers = players;
        player.PlayerGUID = guid;
        return player;
    }
}
