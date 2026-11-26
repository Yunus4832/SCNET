using System.Runtime.InteropServices;

using Engine.Core.Mathematics;

using Game.Managers;
using Game.Subsystems;
using Game.Terrains;
using Game.Terrains.Lighting;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainLightResponseTest
{
    [Fact]
    public void SplitVertexStreamsPreserveTheBytesOfTheInterleavedLayout()
    {
        var lighting = new TerrainLightResponse(
            new Color(1, 2, 3, 4),
            new Color(5, 6, 7, 8),
            new Color(9, 10, 11, 12),
            new Color(13, 14, 15, 16)
        );
        var vertex = new TerrainVertex
        {
            X = 12.5f, Y = 64, Z = -16.5f, Tx = 2000, Ty = -3000, Color = Color.Green,
            Lighting = lighting, emissiveMaterial = new Color(200, 220, 230)
        };
        var geometry = new TerrainGeometryVertex(
            new Vector3(vertex.X, vertex.Y, vertex.Z),
            vertex.Tx,
            vertex.Ty,
            vertex.Color
        );
        var vertexBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref vertex, 1));
        var geometryBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref geometry, 1));
        var stream = new TerrainVertexLighting(lighting, vertex.emissiveMaterial);
        var lightingBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref stream, 1));
        Assert.Equal(TerrainGeometryVertex.VertexDeclaration.VertexStride, geometryBytes.Length);
        Assert.Equal(TerrainVertexLighting.VertexDeclaration.VertexStride, lightingBytes.Length);
        Assert.Equal(TerrainVertex.VertexDeclaration.VertexStride, geometryBytes.Length + lightingBytes.Length);
        Assert.True(vertexBytes[..geometryBytes.Length].SequenceEqual(geometryBytes));
        Assert.True(vertexBytes[geometryBytes.Length..].SequenceEqual(lightingBytes));
    }

    [Fact]
    public void FractionalDaylightInterpolatesAcrossEveryPackedVectorBoundary()
    {
        var response = new TerrainLightResponse(
            new Color(0, 17, 34, 51),
            new Color(68, 85, 102, 119),
            new Color(136, 153, 170, 187),
            new Color(204, 221, 238, 255)
        );
        for (var quarter = 0; quarter <= 60; quarter++)
        {
            var daylight = quarter / 4f;
            Assert.Equal(daylight / 15f, response.GetBrightness(daylight, 0f), 6);
            Assert.Equal(.05f + .95f * daylight / 15f, response.GetBrightness(daylight, .5f), 6);
        }
    }

    [Fact]
    public void ContinuousDaylightPreservesTorchFloorAndUnlitAmbient()
    {
        var torch = TerrainLightResponse.FromChannels(new TerrainLightChannels(0, 15));
        var dark = TerrainLightResponse.FromChannels(default);
        for (var quarter = 0; quarter <= 60; quarter++)
        {
            Assert.Equal(1f, torch.GetBrightness(quarter / 4f, .5f));
            Assert.Equal(.05f, dark.GetBrightness(quarter / 4f, .5f), 6);
        }

        Assert.Equal(5.5f, LightingManager.GetEffectiveLight(new TerrainLightChannels(15, 0), 5.5f));
        Assert.Equal(7f, LightingManager.GetEffectiveLight(new TerrainLightChannels(5, 7), 5.5f));
    }

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
        Assert.Equal(20, Marshal.SizeOf<TerrainVertexLighting>());
        Assert.Equal(20, TerrainVertexLighting.VertexDeclaration.VertexStride);
        Assert.Equal(40, Marshal.SizeOf<TerrainVertex>());
        Assert.Equal(40, TerrainVertex.VertexDeclaration.VertexStride);
        Assert.Equal(36, Marshal.OffsetOf<TerrainVertex>(nameof(TerrainVertex.emissiveMaterial)).ToInt32());
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
