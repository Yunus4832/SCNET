using EntitySystem.TemplatesDatabase;

using Game.Terrains.Generation;

namespace Survivalcraft.Core.Test.Terrains.Generation;

public sealed class TerrainGeneratorSettingsTest
{
    [Theory]
    [InlineData("game:standard")]
    [InlineData("game:flat")]
    public void EveryDeclaredDefaultRoundTripsAlongsideExplicitValues(string id)
    {
        var definition = TerrainGeneratorDefinition.Find(id);
        var source = new TerrainGeneratorSettings(definition);
        source.Set("shape", "island");
        source.Set("islandWidth", 800d);
        var world = new ValuesDictionary();
        source.Save(world);
        var restored = TerrainGeneratorSettings.Load(world);
        Assert.Same(definition, restored.Definition);
        foreach (var parameter in definition.Parameters)
        {
            Assert.Equal(source.Get<object>(parameter.Key), restored.Get<object>(parameter.Key));
        }
    }

    [Theory]
    [InlineData("TerrainGeneratorId", "unknown:generator")]
    [InlineData("TerrainGeneratorVersion", 99)]
    [InlineData("TerrainGeneratorParameters", "bad")]
    public void InvalidWorldHeaderFailsBeforeGeneration(string key, object value)
    {
        var world = new ValuesDictionary();
        new TerrainGeneratorSettings(TerrainGeneratorDefinition.Standard).Save(world);
        world.SetValue(key, value);
        Assert.Throws<InvalidDataException>(() => TerrainGeneratorSettings.Load(world));
    }

    [Theory]
    [InlineData("shape", "round")]
    [InlineData("shape", true)]
    [InlineData("unknown", "island")]
    [InlineData("terrainHeight", 1d)]
    [InlineData("terrainHeight", 32.5d)]
    [InlineData("terrainHeight", double.NaN)]
    [InlineData("surfaceBlock", "missing:block")]
    public void InvalidParameterCannotBeInstalled(string key, object value)
    {
        var settings = new TerrainGeneratorSettings(TerrainGeneratorDefinition.Flat);
        Assert.Throws<InvalidDataException>(() => settings.Set(key, value));
    }

    [Fact]
    public void PartialParameterBagDoesNotAcquireNewDefaultsOnLoad()
    {
        var world = new ValuesDictionary();
        new TerrainGeneratorSettings(TerrainGeneratorDefinition.Flat).Save(world);
        world.SetValue("TerrainGeneratorParameters", new ValuesDictionary { { "shape", "island" } });
        Assert.Throws<InvalidDataException>(() => TerrainGeneratorSettings.Load(world));
    }
}
