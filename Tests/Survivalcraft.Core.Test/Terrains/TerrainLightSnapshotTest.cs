using Game.Terrains;
using Game.Terrains.Lighting;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class TerrainLightSnapshotTest
{
    [Fact]
    public void RecaptureReusesUnchangedSectionsAndOwnsChangedSegments()
    {
        var cells = new byte[65536];
        new Random(719).NextBytes(cells);
        var previous = TerrainLightSnapshot.Capture(cells);
        Assert.Same(previous, TerrainLightSnapshot.Capture(cells, previous));
        cells[TerrainChunk.CalculateCellIndex(8, 67, 9)] ^= 15;
        var current = TerrainLightSnapshot.Capture(cells, previous);
        Assert.NotSame(previous, current);
        Assert.Same(previous.GetSection(3), current.GetSection(3));
        Assert.NotSame(previous.GetSection(4), current.GetSection(4));
    }

    [Fact]
    public void TransmissionSectionsCompressUniformValuesAndOwnMixedBuffers()
    {
        var bytes = new byte[65536];
        Array.Fill(bytes, (byte)1);
        var uniform = new TerrainLightSeed(new TerrainLightSnapshot(default), bytes);
        Assert.Equal(16, uniform.StoredTransmissionBytes);
        var index = TerrainChunk.CalculateCellIndex(5, 67, 9);
        bytes[index] = 255;
        var mixed = new TerrainLightSeed(new TerrainLightSnapshot(default), bytes);
        Assert.Equal(4112, mixed.StoredTransmissionBytes);
        Array.Clear(bytes);
        mixed.CopyTransmissionTo(bytes);
        Assert.Equal(255, bytes[index]);
        Assert.Equal(1, bytes[index + 1]);
        uniform.CopyTransmissionTo(bytes);
        Assert.All(bytes, value => Assert.Equal(1, value));
    }

    [Fact]
    public void ChunkOrderRoundTripsEveryCellAndDistinguishesInteriorFromBorderChanges()
    {
        var source = new byte[65536];
        new Random(719).NextBytes(source);
        var snapshot = TerrainLightSnapshot.Capture(source);
        var result = new byte[65536];
        snapshot.CopyTo(result);
        Assert.Equal(source, result);
        Assert.Equal(TerrainLightChannels.FromPacked(source[67 + (5 << 8) + (9 << 12)]), snapshot.Get(5, 67, 9));
        var original = new TerrainLightSnapshot(default);
        Array.Clear(source);
        source[TerrainChunk.CalculateCellIndex(8, 64, 14)] = 255;
        var interior = TerrainLightSnapshot.Capture(source);
        Assert.False(original.HasSameValues(interior));
        for (var face = 0; face < 4; face++) { Assert.True(original.HasSameBoundary(interior, face)); }

        source[TerrainChunk.CalculateCellIndex(8, 64, 15)] = 255;
        var boundary = TerrainLightSnapshot.Capture(source);
        Assert.False(interior.HasSameBoundary(boundary, 0));
        Assert.True(interior.HasSameBoundary(boundary, 1));
    }

    [Fact]
    public void UniformSkyAndEmptySectionsRequireNoDenseBuffers()
    {
        var light = new TerrainLightChannels(15, 0);
        var snapshot = new TerrainLightSnapshot(light);
        Assert.Equal(0, snapshot.StoredBytes);
        Assert.Equal(light, snapshot.Get(0, 0, 0));
        Assert.Equal(light, snapshot.Get(15, 255, 15));
        Assert.Same(snapshot.GetSection(0), snapshot.GetSection(15));
        var values = new byte[4096];
        snapshot.GetSection(0).CopyTo(values);
        Assert.All(values, value => Assert.Equal(light.Packed, value));
        Assert.Equal(0, TerrainLightSection.Capture(values).StoredBytes);
    }

    [Fact]
    public void PublicationsOwnTheirBuffersAndRetainUnchangedSections()
    {
        var original = new TerrainLightSnapshot(default);
        var buffer = new byte[4096];
        var expected = new TerrainLightChannels(7, 13);
        buffer[3 + (5 << 4) + (9 << 8)] = expected.Packed;
        var section = TerrainLightSection.Capture(buffer);
        var changed = original.WithSection(4, section);
        Array.Fill(buffer, byte.MaxValue);
        Assert.Equal(expected, changed.Get(5, 67, 9));
        Assert.Equal(default, original.Get(5, 67, 9));
        Assert.Equal(default, changed.Get(5, 68, 9));
        Assert.Equal(4096, changed.StoredBytes);
        Assert.Same(original.GetSection(3), changed.GetSection(3));
        section.CopyTo(buffer);
        buffer[3 + (5 << 4) + (9 << 8)] = 0;
        Assert.Equal(expected, changed.Get(5, 67, 9));
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(16, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 256, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(0, 0, 16)]
    public void InvalidCoordinatesDoNotAliasAnotherSection(int x, int y, int z)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainLightSnapshot(default).Get(x, y, z));
    }
}
