using Game;
using Game.Managers;

namespace Survivalcraft.Test.Managers;

public sealed class SettingsManagerTest
{
    [Fact]
    public void EnsureMultiplayerClientIdCreatesMissingValue()
    {
        var settings = new Settings();

        var changed = SettingsManager.EnsureMultiplayerClientId(settings);

        Assert.True(changed);
        Assert.NotEqual(Guid.Empty, settings.MultiplayerClientId);
    }

    [Fact]
    public void EnsureMultiplayerClientIdPreservesExistingValue()
    {
        var id = Guid.NewGuid();
        var settings = new Settings { MultiplayerClientId = id };

        var changed = SettingsManager.EnsureMultiplayerClientId(settings);

        Assert.False(changed);
        Assert.Equal(id, settings.MultiplayerClientId);
    }
}
