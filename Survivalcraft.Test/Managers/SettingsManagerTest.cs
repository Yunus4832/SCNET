using Game;
using Game.Managers;

namespace Survivalcraft.Test.Managers;

public sealed class SettingsManagerTest
{
    [Theory]
    [InlineData("")]
    [InlineData("invalid-token")]
    public void RepairOnlineAccessTokenReplacesInvalidValue(string token)
    {
        var fallbackToken = Guid.NewGuid().ToString("N");
        var settings = new Settings { OnlineAccessToken = token };

        var repaired = SettingsManager.RepairOnlineAccessToken(settings, fallbackToken);

        Assert.True(repaired);
        Assert.Equal(fallbackToken, settings.OnlineAccessToken);
    }

    [Fact]
    public void RepairOnlineAccessTokenPreservesValidValue()
    {
        var token = Guid.NewGuid().ToString("N");
        var settings = new Settings { OnlineAccessToken = token };

        var repaired = SettingsManager.RepairOnlineAccessToken(settings, Guid.NewGuid().ToString("N"));

        Assert.False(repaired);
        Assert.Equal(token, settings.OnlineAccessToken);
    }

    [Fact]
    public void RepairOnlineAccessTokenRejectsInvalidFallback()
    {
        var settings = new Settings { OnlineAccessToken = "invalid-token" };

        Assert.Throws<ArgumentException>(() => SettingsManager.RepairOnlineAccessToken(settings, "invalid-fallback"));
    }
}
