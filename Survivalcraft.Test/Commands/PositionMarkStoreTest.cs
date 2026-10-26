using System.Reflection;
using System.Security.Cryptography;

using Engine.Core.Mathematics;
using Engine.Core.Security;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Protocol.Identity;
using Game.Subsystems;
using Game.World;

namespace Survivalcraft.Test.Commands;

public class PositionMarkStoreTest
{
    [Fact]
    public void WorldMarksRoundTripWithOfflinePlayersAndIndependentScopes()
    {
        var first = Address();
        var second = Address();
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
        Assert.False(restored.GetPrivateMarks(Address()).TryGet("home", out _));
        restored.Load(new ValuesDictionary());
        Assert.Empty(restored.PublicMarks.Names);
        Assert.Empty(restored.GetPrivateMarks(first).Names);
    }

    [Fact]
    public void PositionMarkSubsystemStoresMarksOnlyInWorldDataAndClientSaveDoesNotOverwriteThem()
    {
        using var project = new Project();
        var players = new SubsystemPositionMarks();
        typeof(Subsystem).GetProperty(nameof(Subsystem.Project))!.SetValue(players, project);
        typeof(Subsystem).GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(
            players,
            true
        );
        var guid = Address();
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

    private static PlayerAddress Address() => new(IdentityAddress.Encode(RandomNumberGenerator.GetBytes(32)));
}
