using Engine.Core.Mathematics;

using Game.Terrains;
using Game.Terrains.Generation;
using Game.World;

namespace Survivalcraft.Core.Test.Terrains.Generation;

public sealed class TerrainEnvironmentTest
{
    public static IEnumerable<object[]> Modes()
    {
        return TerrainGeneratorDefinition.All.SelectMany(generator =>
            new[] { "continent", "island" }.Select(shape => new object[] { generator.Id, shape })
        );
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void EnvironmentQueriesNeedNoWorldAndRemainStableAcrossOtherWorlds(string generatorId, string shape)
    {
        var settings = new WorldSettings
            { Generator = new TerrainGeneratorSettings(TerrainGeneratorDefinition.Find(generatorId)) };
        settings.Generator.Set("shape", shape);
        settings.Generator.Set("islandLength", 600d);
        settings.Generator.Set("temperatureOffset", 2d);
        settings.Generator.Set("humidityOffset", -3d);
        var environment = TerrainEnvironment.Create(settings, 719);
        Assert.False(environment is ITerrainContentsGenerator);
        var before = Sample(environment);
        Assert.All(before, value => Assert.True(float.IsFinite(value)));

        foreach (var other in TerrainGeneratorDefinition.All)
        {
            var independent = TerrainEnvironment.Create(
                new WorldSettings { Generator = new TerrainGeneratorSettings(other) },
                -843
            );
            Sample(independent);
        }

        Assert.Equal(before, Sample(environment));
        Assert.Equal(before, Sample(TerrainEnvironment.Create(settings, 719)));
    }

    [Theory]
    [InlineData("continent", 0, 0, 199)]
    [InlineData("island", 202, 0, -1)]
    public void FlatWorldQueriesPreserveShoreAndClimateSemantics(
        string shape,
        float x,
        float z,
        float shoreDistance
    )
    {
        var settings = new WorldSettings { Generator = new TerrainGeneratorSettings(TerrainGeneratorDefinition.Flat) };
        settings.Generator.Set("shape", shape);
        settings.Generator.Set("shoreRoughness", 0d);
        settings.Generator.Set("terrainHeight", 81d);
        settings.Generator.Set("seaLevelOffset", -4d);
        settings.Generator.Set("temperatureOffset", 9d);
        settings.Generator.Set("humidityOffset", -16d);
        var environment = TerrainEnvironment.Create(settings, 17);

        Assert.Equal(shoreDistance, environment.CalculateOceanShoreDistance(x, z));
        Assert.Equal(81, environment.CalculateHeight(x, z));
        Assert.Equal(77, environment.OceanLevel);
        Assert.Equal(15, environment.CalculateTemperature(x, z));
        Assert.Equal(0, environment.CalculateHumidity(x, z));
        Assert.Equal(0, environment.CalculateMountainRangeFactor(x, z));
    }

    private static float[] Sample(ITerrainEnvironment environment)
    {
        var values = new List<float> { environment.OceanLevel };
        foreach (var position in new[] { new Vector2(-2400, 875), Vector2.Zero, new Vector2(1520, -3100) })
        {
            values.Add(environment.CalculateOceanShoreDistance(position.X, position.Y));
            values.Add(environment.CalculateHeight(position.X, position.Y));
            values.Add(environment.CalculateMountainRangeFactor(position.X, position.Y));
            values.Add(environment.CalculateTemperature(position.X, position.Y));
            values.Add(environment.CalculateHumidity(position.X, position.Y));
        }

        return values.ToArray();
    }
}
