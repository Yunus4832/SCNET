using System.Runtime.InteropServices;

using Game.Managers;
using Game.Subsystems;
using Game.Terrains;
using Game.Terrains.Lighting;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainLightResponseTest
{
    [Fact]
    public void MissingPublishedSamplesReturnUnknownWithoutRequiringAWorldClock()
    {
        using var terrain = new Terrain();
        var subsystem = new SubsystemTerrain { Terrain = terrain };
        Assert.Null(LightingManager.CalculateSmoothLight(subsystem, new Engine.Core.Mathematics.Vector3(2, 65, 2)));
        subsystem.Dispose();
    }

    [Fact]
    public void ResponseUsesSixteenBytesAndPreservesEveryChannelCombination()
    {
        Assert.Equal(16, Marshal.SizeOf<TerrainLightResponse>());
        Assert.Equal(36, Marshal.SizeOf<TerrainVertex>());
        Assert.Equal(36, TerrainVertex.VertexDeclaration.VertexStride);
        Assert.Equal(20, Marshal.OffsetOf<TerrainVertex>(nameof(TerrainVertex.Lighting)).ToInt32());
        for (var packed = 0; packed < 256; packed++)
        {
            var channels = TerrainLightChannels.FromPacked((byte)packed);
            var response = TerrainLightResponse.FromChannels(channels);
            for (var daylight = 0; daylight < 16; daylight++)
            {
                var level = channels.GetEffectiveLight(new TerrainLightEnvironment(daylight));
                var expected = MathF.Pow(level / 15f, 1.25f);
                Assert.InRange(MathF.Abs(response.GetBrightness(daylight, 0f) - expected), 0f, 0.5f / 255f + 0.000001f);
            }
        }
    }

    [Fact]
    public void InterpolationPreservesBrightTorchAndOpenSkyInsteadOfAveragingTheirChannels()
    {
        TerrainLightChannels[] samples = [new(0, 15), new(15, 0)];
        float[] weights = [0.5f, 0.5f];
        var response = TerrainLightResponse.FromSamples(samples, weights);
        Assert.Equal(1f, response.GetBrightness(15, 0f));
        Assert.InRange(MathF.Abs(response.GetBrightness(0, 0f) - .5f), 0f, 0.5f / 255f + 0.000001f);
    }

    [Fact]
    public void EightCornerInterpolationAndDisplayBrightnessMatchTheExistingOrder()
    {
        TerrainLightChannels[] samples =
            [new(15, 0), new(2, 14), new(0, 0), new(8, 9), new(3, 0), new(0, 7), new(11, 5), new(4, 4)];
        var position = new Engine.Core.Mathematics.Vector3(.27f, .61f, .83f);
        var weights = new float[8];
        for (var i = 0; i < weights.Length; i++)
        {
            weights[i] = ((i & 4) == 0 ? 1 - position.X : position.X) *
                         ((i & 2) == 0 ? 1 - position.Y : position.Y) *
                         ((i & 1) == 0 ? 1 - position.Z : position.Z);
        }

        var response = TerrainLightResponse.FromSamples(samples, weights);
        for (var daylight = 0; daylight < 16; daylight++)
        {
            for (var setting = 0; setting <= 10; setting++)
            {
                var brightness = setting / 10f;
                var ambient = 0.1f * brightness;
                var expected = 0f;
                for (var i = 0; i < samples.Length; i++)
                {
                    var light = samples[i].GetEffectiveLight(new TerrainLightEnvironment(daylight));
                    expected += weights[i] * (ambient + (1 - ambient) * MathF.Pow(light / 15f, 1.25f));
                }

                Assert.InRange(
                    MathF.Abs(response.GetBrightness(daylight, brightness) - expected),
                    0f,
                    .5f / 255f + 0.000001f
                );
            }
        }
    }
}
