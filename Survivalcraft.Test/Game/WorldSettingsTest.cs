using EntitySystem.TemplatesDatabase;

using Game;

namespace Survivalcraft.Test.World;

public sealed class WorldSettingsTest
{
    [Theory]
    [InlineData(10, 10)]
    [InlineData(20, 20)]
    [InlineData(32, 32)]
    [InlineData(100, 32)]
    [InlineData(65535, 32)]
    public void PlayerLimitUsesSameBoundForAssignmentAndPersistence(int requested, int expected)
    {
        var settings = new WorldSettings { MaxOnlinePlayerCount = (ushort)requested };
        Assert.Equal(expected, settings.MaxOnlinePlayerCount);

        var values = new ValuesDictionary();
        values.SetValue("WorldName", "LimitTest");
        values.SetValue("MaxOnlinePlayerCount", (ushort)requested);
        settings.Load(values);
        Assert.Equal(expected, settings.MaxOnlinePlayerCount);

        settings.Save(values, false);
        Assert.Equal((ushort)expected, values.GetValue<ushort>("MaxOnlinePlayerCount"));
    }

    [Fact]
    public void DefaultPlayerLimitRemainsTwenty()
    {
        Assert.Equal(20, new WorldSettings().MaxOnlinePlayerCount);
    }
}
