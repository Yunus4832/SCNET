using Game.Managers;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Managers;

[Collection(ConfigFileCollection.Name)]
public sealed class SettingsManagerTest
{
    [Fact]
    public void RuntimeBrightnessChangesAreClampedAndNotifiedByTheManager()
    {
        var previous = SettingsManager.Current.Brightness;
        var changes = 0;

        void Changed()
        {
            changes++;
        }

        SettingsManager.BrightnessChanged += Changed;
        try
        {
            SettingsManager.Current.Brightness = 0.5f;
            Assert.Equal(0, changes);
            SettingsManager.SetBrightness(2f);
            Assert.Equal(1f, SettingsManager.Current.Brightness);
            Assert.Equal(1, changes);
            SettingsManager.SetBrightness(3f);
            Assert.Equal(1, changes);
            SettingsManager.SetBrightness(-1f);
            Assert.Equal(0f, SettingsManager.Current.Brightness);
            Assert.Equal(2, changes);
        }
        finally
        {
            SettingsManager.BrightnessChanged -= Changed;
            SettingsManager.Current.Brightness = previous;
        }
    }
}
