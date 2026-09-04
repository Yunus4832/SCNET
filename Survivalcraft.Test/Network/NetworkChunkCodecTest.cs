using Game.Network;
using Game.Network.Serialization;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Network;

public sealed class NetworkChunkCodecTest
{
    [Fact]
    public void CodecRemovesLightAndRebuildableShaftFields()
    {
        using var terrain = new Terrain();
        var source = new TerrainChunk(terrain, 3, -2);
        for (var z = 0; z < 16; z++)
        {
            for (var x = 0; x < 16; x++)
            {
                var shaft = Terrain.ReplaceTemperature(0, (x + z) & 0xF);
                shaft = Terrain.ReplaceHumidity(shaft, (x * 3 + z) & 0xF);
                shaft = Terrain.ReplaceTopHeight(shaft, 200);
                shaft = Terrain.ReplaceBottomHeight(shaft, 12);
                shaft = Terrain.ReplaceSunlightHeight(shaft, 180);
                source.SetShaftValueFast(x, z, shaft);
            }
        }

        for (var y = 0; y < 256; y++)
        {
            for (var z = 0; z < 16; z++)
            {
                for (var x = 0; x < 16; x++)
                {
                    var contents = y < 60 ? 3 : 0;
                    source.SetCellValueFast(x, y, z, Terrain.MakeBlockValue(contents, (x + y + z) & 0xF, y & 7));
                }
            }
        }

        var encoded = NetworkChunkCodec.Encode(source);
        var decoded = NetworkChunkCodec.Decode(source.Coords, encoded.Payload);

        Assert.True(encoded.Payload.Length < source.Cells.Length * sizeof(int) / 20);
        for (var i = 0; i < source.Cells.Length; i++)
        {
            Assert.Equal(Terrain.ReplaceLight(source.Cells[i], 0), decoded.Cells[i]);
        }

        for (var z = 0; z < 16; z++)
        {
            for (var x = 0; x < 16; x++)
            {
                Assert.Equal(source.GetTemperatureFast(x, z), decoded.GetTemperatureFast(x, z));
                Assert.Equal(source.GetHumidityFast(x, z), decoded.GetHumidityFast(x, z));
                Assert.Equal(0, decoded.GetTopHeightFast(x, z));
                Assert.Equal(0, decoded.GetBottomHeightFast(x, z));
                Assert.Equal(0, decoded.GetSunlightHeightFast(x, z));
            }
        }
    }

    [Fact]
    public void ContentRevisionIgnoresLightButTracksNetworkContent()
    {
        using var terrain = new Terrain();
        var chunk = new TerrainChunk(terrain, 0, 0);
        var revision = chunk.NetworkContentRevision;

        chunk.SetCellValueFast(0, 0, 0, Terrain.MakeBlockValue(0, 15, 0));
        Assert.Equal(revision, chunk.NetworkContentRevision);

        chunk.SetCellValueFast(0, 0, 0, Terrain.MakeBlockValue(1, 15, 0));
        Assert.True(chunk.NetworkContentRevision > revision);
    }

    [Fact]
    public void CacheReusesStablePayloadAndReplacesChangedPayload()
    {
        using var terrain = new Terrain();
        var chunk = new TerrainChunk(terrain, 0, 0);
        var cache = new NetworkChunkCache();

        var firstSnapshot = Snapshot(chunk);
        var first = cache.GetOrEncode(firstSnapshot);
        var second = cache.GetOrEncode(firstSnapshot);
        Assert.Same(first.Payload, second.Payload);

        chunk.SetCellValueFast(0, 0, 0, Terrain.MakeBlockValue(2));
        var changed = cache.GetOrEncode(Snapshot(chunk));
        Assert.NotSame(first.Payload, changed.Payload);
    }

    private static AuthorityChunkSnapshot Snapshot(TerrainChunk chunk) => new(
        chunk.Coords,
        chunk.NetworkContentRevision + 1,
        chunk.Cells.ToArray(),
        chunk.Shafts.ToArray());
}
