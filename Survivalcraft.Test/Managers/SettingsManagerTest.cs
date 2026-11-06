using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Managers;

[Collection(ConfigFileCollection.Name)]
public sealed class SettingsManagerTest
{
    [Fact]
    public void RuntimeBrightnessChangesAreClampedAndNotifiedByTheManager()
    {
        var previous = Game.Configuration.ClientConfiguration.Preferences.Brightness;
        var changes = 0;

        void Changed()
        {
            changes++;
        }

        Game.Configuration.ClientConfiguration.BrightnessChanged += Changed;
        try
        {
            Game.Configuration.ClientConfiguration.Preferences.Brightness = 0.5f;
            Assert.Equal(0, changes);
            Game.Configuration.ClientConfiguration.SetBrightness(2f);
            Assert.Equal(1f, Game.Configuration.ClientConfiguration.Preferences.Brightness);
            Assert.Equal(1, changes);
            Game.Configuration.ClientConfiguration.SetBrightness(3f);
            Assert.Equal(1, changes);
            Game.Configuration.ClientConfiguration.SetBrightness(-1f);
            Assert.Equal(0f, Game.Configuration.ClientConfiguration.Preferences.Brightness);
            Assert.Equal(2, changes);
        }
        finally
        {
            Game.Configuration.ClientConfiguration.BrightnessChanged -= Changed;
            Game.Configuration.ClientConfiguration.Preferences.Brightness = previous;
        }
    }
}
