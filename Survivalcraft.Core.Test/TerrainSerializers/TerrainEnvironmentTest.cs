using Engine.Core.Mathematics;

using Game.Terrains;
using Game.TerrainSerializers;
using Game.World;

namespace Survivalcraft.Core.Test.TerrainSerializers;

public sealed class TerrainEnvironmentTest
{
    public static IEnumerable<object[]> Modes()
    {
        return Enum.GetValues<TerrainGenerationMode>().Select(mode => new object[] { mode });
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void EnvironmentQueriesNeedNoWorldAndRemainStableAcrossOtherWorlds(TerrainGenerationMode mode)
    {
        var settings = new WorldSettings
        {
            TerrainGenerationMode = mode,
            IslandSize = new Vector2(400, 600),
            TemperatureOffset = 2,
            HumidityOffset = -3,
            BiomeSize = 1.25f,
            ShoreRoughness = 0.5f
        };
        var environment = TerrainEnvironment.Create(settings, 719);
        Assert.False(environment is ITerrainContentsGenerator);
        var before = Sample(environment);
        Assert.All(before, value => Assert.True(float.IsFinite(value)));

        foreach (var other in Enum.GetValues<TerrainGenerationMode>())
        {
            var independent = TerrainEnvironment.Create(new WorldSettings { TerrainGenerationMode = other }, -843);
            Sample(independent);
        }

        Assert.Equal(before, Sample(environment));
        Assert.Equal(before, Sample(TerrainEnvironment.Create(settings, 719)));
    }

    [Theory]
    [InlineData(TerrainGenerationMode.FlatContinent, 0, 0, 199)]
    [InlineData(TerrainGenerationMode.FlatIsland, 202, 0, -1)]
    [InlineData(TerrainGenerationMode.LegacyFlatContinentPre21, 0, 0, -2001)]
    [InlineData(TerrainGenerationMode.LegacyFlatIslandPre21, 2402, 2200, -1)]
    public void FlatWorldQueriesPreserveShoreAndClimateSemantics(
        TerrainGenerationMode mode,
        float x,
        float z,
        float shoreDistance
    )
    {
        var environment = TerrainEnvironment.Create(
            new WorldSettings
            {
                TerrainGenerationMode = mode,
                IslandSize = new Vector2(400, 400),
                ShoreRoughness = 0,
                TerrainLevel = 81,
                SeaLevelOffset = -4,
                TemperatureOffset = 9,
                HumidityOffset = -20
            },
            17
        );

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
