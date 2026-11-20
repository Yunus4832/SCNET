using EntitySystem.TemplatesDatabase;

using Game.World;

namespace Survivalcraft.Core.Test.World;

public class CreativeWorldRulesTest
{
    [Fact]
    public void SeparateWorldsOwnIndependentCreativeRules()
    {
        var first = new WorldSettings { CreativeDigTime = 0.1f, CreativeReach = 5f };
        var second = new WorldSettings { CreativeDigTime = 0.3f, CreativeReach = 10f };
        first.CreativeDigTime = 0.15f;
        first.CreativeReach = 6f;
        Assert.Equal(0.3f, second.CreativeDigTime);
        Assert.Equal(10f, second.CreativeReach);
        Assert.Equal(0.15f, first.CreativeDigTime);
        Assert.Equal(6f, first.CreativeReach);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WorldSaveAndLiveSettingsRetainCreativeRules(bool liveOnly)
    {
        var authority = new WorldSettings { Name = "World", CreativeDigTime = 0.275f, CreativeReach = 9f };
        var saved = new ValuesDictionary();
        authority.Save(saved, liveOnly);
        var reloaded = new WorldSettings();
        reloaded.Load(saved);
        Assert.Equal(0.275f, reloaded.CreativeDigTime);
        Assert.Equal(9f, reloaded.CreativeReach);
    }

    [Fact]
    public void InstallingAuthoritativeWorldValuesReplacesClientDefaultsWithoutSharingMutableSettings()
    {
        var authority = new WorldSettings { Name = "Authority", CreativeDigTime = 0.125f, CreativeReach = 8.5f };
        var worldValues = new ValuesDictionary();
        authority.Save(worldValues, false);
        var client = new WorldSettings { CreativeDigTime = 0.3f, CreativeReach = 5f };
        client.Load(worldValues);
        Assert.Equal(authority.CreativeDigTime, client.CreativeDigTime);
        Assert.Equal(authority.CreativeReach, client.CreativeReach);
        client.CreativeDigTime = 0.2f;
        client.CreativeReach = 7.5f;
        Assert.Equal(0.125f, authority.CreativeDigTime);
        Assert.Equal(8.5f, authority.CreativeReach);
    }

    [Fact]
    public void MissingSavedRulesUseTheEstablishedDefaults()
    {
        var defaults = new WorldSettings();
        var saved = new WorldSettings();
        saved.Load(new ValuesDictionary { { "WorldName", "World" } });
        Assert.Equal(0.2f, defaults.CreativeDigTime);
        Assert.Equal(7.5f, defaults.CreativeReach);
        Assert.Equal(defaults.CreativeDigTime, saved.CreativeDigTime);
        Assert.Equal(defaults.CreativeReach, saved.CreativeReach);
    }
}
