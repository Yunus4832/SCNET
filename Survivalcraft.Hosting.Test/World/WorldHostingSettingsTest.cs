using EntitySystem.TemplatesDatabase;

using Game.Modding;
using Game.Servers;
using Game.World;

namespace Survivalcraft.Hosting.Test.World;

public class WorldHostingSettingsTest
{
    [Fact]
    public void HostingAndRulesWriteTheirOwnKeysToTheSameWorldRecord()
    {
        var rules = new WorldSettings { Name = "World", TimeOfYear = 0.75f };
        var hosting = new WorldHostingSettings
        {
            RunServer = true,
            ModProfileResolutionStrategy = ModProfileResolutionStrategy.WorldOnly,
            P2P = new P2PRoomSettings { Enabled = true, IsPublic = false }
        };
        var values = new ValuesDictionary();
        rules.Save(values, false);
        hosting.Save(values, rules.Name);
        var loadedRules = new WorldSettings();
        loadedRules.Load(values);
        var loadedHosting = new WorldHostingSettings();
        loadedHosting.Load(values);
        Assert.Equal(0.75f, loadedRules.TimeOfYear);
        Assert.True(loadedHosting.RunServer);
        Assert.Equal(ModProfileResolutionStrategy.WorldOnly, loadedHosting.ModProfileResolutionStrategy);
        Assert.True(loadedHosting.P2P.Enabled);
        Assert.False(loadedHosting.P2P.IsPublic);
        Assert.Equal("World", loadedHosting.P2P.Name);
        Assert.Null(loadedHosting.ServerIdentityAddress);
        Assert.False(values.ContainsKey("PrivateKey"));
    }

    [Fact]
    public void MissingHostingValuesUseTheExistingWorldDefaults()
    {
        var settings = new WorldHostingSettings();
        settings.Load(new ValuesDictionary());
        Assert.False(settings.RunServer);
        Assert.Null(settings.ServerIdentityAddress);
        Assert.False(settings.P2P.Enabled);
        Assert.Equal(ModProfileResolutionStrategy.GlobalPlusWorld, settings.ModProfileResolutionStrategy);
    }
}
