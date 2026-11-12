using Game.Terrains.Lighting;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class TerrainLightChannelsTest
{
    [Fact]
    public void PackedChannelsRoundTripEveryValueAndKeepEmittedLightIndependentOfNight()
    {
        for (var sky = 0; sky < 16; sky++)
        {
            for (var block = 0; block < 16; block++)
            {
                var light = new TerrainLightChannels(sky, block);
                Assert.Equal(sky, light.Sky);
                Assert.Equal(block, light.Block);
                Assert.Equal(light, TerrainLightChannels.FromPacked(light.Packed));
                Assert.Equal(block, light.GetEffectiveLight(new TerrainLightEnvironment(0)));
                Assert.Equal(Math.Max(sky, block), light.GetEffectiveLight(new TerrainLightEnvironment(15)));
            }
        }
    }

    [Fact]
    public void SpatialAttenuationMatchesSeparateSourcesAtEveryDaylightStrength()
    {
        for (var daylight = 0; daylight < 16; daylight++)
        {
            var environment = new TerrainLightEnvironment(daylight);
            for (var emitted = 0; emitted < 16; emitted++)
            {
                for (var skyLoss = 0; skyLoss < 17; skyLoss++)
                {
                    for (var blockLoss = 0; blockLoss < 17; blockLoss++)
                    {
                        var sky = Math.Max(0, 15 - skyLoss);
                        var block = Math.Max(0, emitted - blockLoss);
                        var expected = Math.Max(Math.Max(0, daylight - skyLoss), block);
                        Assert.Equal(expected, new TerrainLightChannels(sky, block).GetEffectiveLight(environment));
                    }
                }
            }
        }
    }

    [Fact]
    public void CubeMaximumAndShadowCommuteWithEnvironmentCombination()
    {
        for (var first = 0; first < 256; first++)
        {
            var a = TerrainLightChannels.FromPacked((byte)first);
            for (var second = 0; second < 256; second++)
            {
                var b = TerrainLightChannels.FromPacked((byte)second);
                for (var daylight = 0; daylight < 16; daylight++)
                {
                    var environment = new TerrainLightEnvironment(daylight);
                    var maximum = Math.Max(a.GetEffectiveLight(environment), b.GetEffectiveLight(environment));
                    var combined = a.Max(b);
                    Assert.Equal(maximum, combined.GetEffectiveLight(environment));
                    for (var shadow = 0; shadow < 16; shadow++)
                    {
                        Assert.Equal(
                            Math.Max(0, maximum - shadow),
                            combined.Attenuate(shadow).GetEffectiveLight(environment)
                        );
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(16, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 16)]
    public void InvalidChannelsAreRejected(int sky, int block)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainLightChannels(sky, block));
    }
}
