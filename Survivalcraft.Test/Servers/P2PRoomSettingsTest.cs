using Game;
using Game.Managers;
using Game.Servers;

namespace Survivalcraft.Test.Servers;

public sealed class P2PRoomSettingsTest
{
    [Fact]
    public void WorldOwnsCopiedServiceConfigurationAfterInstallationChanges()
    {
        var installed = new InstalledRendezvousService { Name = "Original", ControlUrl = "https://original.example/" };
        var manager = new RendezvousManager(new RendezvousSettings { Services = [installed] }, _ => { });
        var room = new P2PRoomSettings { Enabled = true, Name = "Room", Description = "Description", IsPublic = true }
            .SelectService(manager.Snapshot()[0]);
        manager.Edit(installed with { Name = "Updated", ControlUrl = "https://updated.example/", IsEnabled = false });
        manager.Delete(installed.Id);
        Assert.Equal("Original", room.ServiceName);
        Assert.Equal("https://original.example/", room.ControlUrl);
        Assert.Equal(room, P2PRoomSettings.Load(room.Save("World")));
    }

    [Fact]
    public void RoomDefaultsAreDisabledAndWorldNameIsPersistedAsInitialRoomName()
    {
        var room = new P2PRoomSettings();
        var loaded = P2PRoomSettings.Load(room.Save("First world"));
        Assert.False(loaded.Enabled);
        Assert.False(loaded.IsPublic);
        Assert.Equal("First world", loaded.Name);
        Assert.Equal("First world", P2PRoomSettings.Load(loaded.Save("Renamed world")).Name);
        Assert.Empty(loaded.ControlUrl);
    }

    [Fact]
    public void SessionOverridesPreserveExplicitFalseAndEmptyDescriptionWithoutChangingWorld()
    {
        var world = new P2PRoomSettings
        {
            Enabled = true,
            ControlUrl = "https://world.example/",
            ServiceName = "World service",
            Description = "Original",
            IsPublic = true
        };
        var restored = new P2PRoomOverrides { Enabled = true, Name = "Saved room" };
        var effective = restored.Merge(
            new P2PRoomOverrides
            {
                Enabled = false,
                ControlUrl = "https://session.example/",
                Description = string.Empty,
                IsPublic = false
            }
        ).Apply(world, "World");
        Assert.False(effective.Enabled);
        Assert.False(effective.IsPublic);
        Assert.Empty(effective.Description);
        Assert.Empty(effective.ServiceName);
        Assert.Equal("Saved room", effective.Name);
        Assert.Equal("https://session.example/", effective.ControlUrl);
        Assert.True(world.Enabled);
        Assert.Equal("Original", world.Description);
        Assert.False(
            new P2PRoomOverrides { ControlUrl = "https://session.example/" }
                .Apply(new P2PRoomSettings(), "World").Enabled
        );
    }
}
