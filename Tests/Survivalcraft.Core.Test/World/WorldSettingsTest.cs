using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Terrains.Generation;
using Game.World;

namespace Survivalcraft.Core.Test.World;

public class WorldSettingsTest
{
    [Fact]
    public void SavedLegacyTerrainModeIsRejected()
    {
        var values = new ValuesDictionary
        {
            { "WorldName", "Legacy" },
            { "TerrainGenerationMode", "LegacyContinent23" }
        };

        var error = Assert.Throws<InvalidDataException>(() => new WorldSettings().Load(values));
        Assert.Contains("TerrainGeneratorId", error.Message);
    }

    [Fact]
    public void NewWorldAndMissingSavedFieldsKeepTheirDistinctDefaults()
    {
        var created = new WorldSettings();
        var loaded = new WorldSettings();
        var values = new ValuesDictionary { { "WorldName", "Saved" } };
        created.Generator.Save(values);
        loaded.Load(values);
        Assert.Equal(GameMode.Survival, created.GameMode);
        Assert.Equal(GameMode.Challenging, loaded.GameMode);
        Assert.Equal(400d, loaded.Generator.Get<double>("islandWidth"));
        Assert.Equal(0.125f, created.TimeOfYear);
        Assert.Equal(created.TimeOfYear, loaded.TimeOfYear);
    }

    [Fact]
    public void RulesPersistStateButDoNotOwnHostingKeys()
    {
        var settings = new WorldSettings { Name = "World", TimeOfYear = 0.75f, KeywordBlocking = "one;two" };
        var values = new ValuesDictionary();
        settings.Save(values, false);
        foreach (var key in new[] { "RunServer", "P2P", "ServerIdentityAddress", "ModProfileResolutionStrategy" })
        {
            Assert.False(values.ContainsKey(key));
        }

        var loaded = new WorldSettings();
        loaded.Load(values);
        Assert.Equal(0.75f, loaded.TimeOfYear);
        Assert.Equal(settings.KeywordBlocking, loaded.KeywordBlocking);
        Assert.Equal(settings.MaxOnlinePlayerCount, loaded.MaxOnlinePlayerCount);
    }

    [Fact]
    public void LiveUpdateDoesNotOverwriteGenerationInputs()
    {
        var values = new ValuesDictionary { { "WorldSeedString", "original" } };
        var original = new TerrainGeneratorSettings(TerrainGeneratorDefinition.Flat);
        original.Set("terrainHeight", 80d);
        original.Save(values);
        new WorldSettings { Name = "Updated", Seed = "replacement" }.Save(values, true);
        Assert.Equal("original", values.GetValue<string>("WorldSeedString"));
        Assert.Equal(80d, TerrainGeneratorSettings.Load(values).Get<double>("terrainHeight"));
        Assert.Equal("Updated", values.GetValue<string>("WorldName"));
    }

    [Fact]
    public void PaletteUsesExplicitFallbackAndKeepsCopiesIndependent()
    {
        var palette = new WorldPalette();
        Assert.Equal("Localized color", palette.GetName(0, "Localized color"));
        palette.Names[0] = "Custom";
        var copy = new WorldPalette();
        palette.CopyTo(copy);
        palette.Names[0] = "Changed";
        Assert.Equal("Custom", copy.GetName(0, "Fallback"));
        var restored = new WorldPalette(copy.Save());
        Assert.Equal(copy.Colors, restored.Colors);
        Assert.Equal(copy.Names, restored.Names);
    }
}
