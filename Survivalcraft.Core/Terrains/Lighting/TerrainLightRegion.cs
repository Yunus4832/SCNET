using System.Buffers;

namespace Game.Terrains.Lighting;

/// <summary>从直接光照种子计算一批发光变化，无需邻区先发布中间结果。</summary>
internal sealed class TerrainLightRegion(
    TerrainLightingStore.LocalLightBatch batch,
    int seedX,
    int seedZ,
    int heightLimit,
    TerrainLightSeed?[] seeds,
    TerrainChunk?[] chunks,
    TerrainLightSnapshot?[] previous
)
{
    private const int _height = 256;

    private const int _width = 80;

    private const int _zStride = _width * _height;

    private const int _cellCount = _width * _zStride;

    internal TerrainLightingStore.LocalLightBatch Batch { get; } = batch;

    internal static TerrainLightRegion? Capture(
        Terrain terrain,
        TerrainLightingStore.LocalLightBatch batch,
        bool clipAboveTerrain
    )
    {
        // A chunk of direct seeds beyond the output rectangle covers the maximum 14 propagation steps.
        var seedX = ((batch.Source.X - 15) >> 4) - 1;
        var seedZ = ((batch.Source.Z - 15) >> 4) - 1;
        var chunks = new TerrainChunk?[25];
        var seeds = new TerrainLightSeed?[25];
        var top = 0;
        for (var z = 0; z < 5; z++)
        {
            for (var x = 0; x < 5; x++)
            {
                if (terrain.GetChunkAtCoords(seedX + x, seedZ + z) is not { HasContents: true } chunk) { continue; }

                if (chunk.NeedsContentValidation ||
                    !chunk.DerivedRevisions.HasCurrentLightSeed ||
                    !terrain.Lighting.TryGetSeed(chunk, out var seed))
                {
                    return null;
                }

                var slot = x + 5 * z;
                chunks[slot] = chunk;
                seeds[slot] = seed;
                if (!clipAboveTerrain) { continue; }

                for (var dx = 0; dx < 16; dx++)
                {
                    for (var dz = 0; dz < 16; dz++) { top = Math.Max(top, chunk.GetTopHeightFast(dx, dz)); }
                }
            }
        }

        var previous = new TerrainLightSnapshot?[batch.Members.Length];
        for (var i = 0; i < batch.Members.Length; i++)
        {
            var chunk = batch.Members[i];
            var x = chunk.Coords.X - seedX;
            var z = chunk.Coords.Y - seedZ;
            if (x is < 1 or > 3 ||
                z is < 1 or > 3 ||
                !ReferenceEquals(chunks[x + 5 * z], chunk) ||
                !ReferenceEquals(terrain.Lighting.GetLocalBatch(chunk), batch))
            {
                return null;
            }

            terrain.Lighting.TryGetPublishedSnapshot(chunk, out previous[i]);
        }

        return new TerrainLightRegion(
            batch,
            seedX,
            seedZ,
            clipAboveTerrain ? Math.Min(_height, top + 15) : _height,
            seeds,
            chunks,
            previous
        );
    }

    internal bool IsCurrent(Terrain terrain)
    {
        foreach (var member in Batch.Members)
        {
            if (!ReferenceEquals(terrain.Lighting.GetLocalBatch(member), Batch)) { return false; }
        }

        for (var z = 0; z < 5; z++)
        {
            for (var x = 0; x < 5; x++)
            {
                var slot = x + 5 * z;
                var chunk = terrain.GetChunkAtCoords(seedX + x, seedZ + z);
                var loaded = chunk is { HasContents: true } ? chunk : null;
                if (!ReferenceEquals(loaded, chunks[slot])) { return false; }

                if (loaded != null &&
                    (loaded.NeedsContentValidation ||
                     !loaded.DerivedRevisions.HasCurrentLightSeed ||
                     !terrain.Lighting.TryGetSeed(loaded, out var seed) ||
                     !ReferenceEquals(seed, seeds[slot])))
                {
                    return false;
                }
            }
        }

        return true;
    }

    internal TerrainLightSnapshot[] Compute(CancellationToken cancellation)
    {
        var pool = ArrayPool<byte>.Shared;
        var values = pool.Rent(_cellCount);
        var loss = pool.Rent(_cellCount);
        var chunkValues = pool.Rent(65536);
        var chunkLoss = pool.Rent(65536);
        try
        {
            values.AsSpan(0, _cellCount).Clear();
            loss.AsSpan(0, _cellCount).Fill(255);
            for (var slot = 0; slot < seeds.Length; slot++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (seeds[slot] is not { } seed) { continue; }

                seed.Light.CopyTo(chunkValues.AsSpan(0, 65536));
                seed.CopyTransmissionTo(chunkLoss.AsSpan(0, 65536));
                var start = slot / 5 * 16 * _zStride + slot % 5 * 16 * _height;
                for (var z = 0; z < 16; z++)
                {
                    var inputIndex = z * 16 * _height;
                    var output = start + z * _zStride;
                    chunkValues.AsSpan(inputIndex, 16 * _height).CopyTo(values.AsSpan(output));
                    chunkLoss.AsSpan(inputIndex, 16 * _height).CopyTo(loss.AsSpan(output));
                }
            }

            var queue = new Queue<int>();
            for (var z = 0; z < _width; z++)
            {
                cancellation.ThrowIfCancellationRequested();
                for (var x = 0; x < _width; x++)
                {
                    for (var y = 0; y < heightLimit; y++)
                    {
                        var index = z * _zStride + x * _height + y;
                        var source = values[index];
                        if ((source & 15) != 0) { queue.Enqueue(index); }

                        var currentSky = source >> 4;
                        var attenuation = loss[index];
                        if (currentSky >= 14 || attenuation >= 15) { continue; }

                        var sky = 0;
                        if (y > 0) { sky = Math.Max(sky, values[index - 1] >> 4); }

                        if (y < _height - 1) { sky = Math.Max(sky, values[index + 1] >> 4); }

                        if (x > 0) { sky = Math.Max(sky, values[index - _height] >> 4); }

                        if (x < _width - 1) { sky = Math.Max(sky, values[index + _height] >> 4); }

                        if (z > 0) { sky = Math.Max(sky, values[index - _zStride] >> 4); }

                        if (z < _width - 1) { sky = Math.Max(sky, values[index + _zStride] >> 4); }

                        if (sky - attenuation <= currentSky) { continue; }

                        values[index] = (byte)(((sky - attenuation) << 4) | (source & 15));
                        queue.Enqueue(index);
                    }
                }
            }

            var processed = 0;
            while (queue.TryDequeue(out var index))
            {
                if ((processed++ & 1023) == 0) { cancellation.ThrowIfCancellationRequested(); }

                var y = index % _height;
                var x = index / _height % _width;
                var z = index / _zStride;
                var source = values[index];
                if (y > 0) { Increase(index - 1, source); }

                if (y < _height - 1) { Increase(index + 1, source); }

                if (x > 0) { Increase(index - _height, source); }

                if (x < _width - 1) { Increase(index + _height, source); }

                if (z > 0) { Increase(index - _zStride, source); }

                if (z < _width - 1) { Increase(index + _zStride, source); }
            }

            var result = new TerrainLightSnapshot[Batch.Members.Length];
            for (var i = 0; i < Batch.Members.Length; i++)
            {
                var target = Batch.Members[i];
                var origin = (target.Coords.Y - seedZ) * 16 * _zStride +
                             (target.Coords.X - seedX) * 16 * _height;
                for (var z = 0; z < 16; z++)
                {
                    values.AsSpan(origin + z * _zStride, 16 * _height)
                        .CopyTo(chunkValues.AsSpan(z * 16 * _height));
                }

                result[i] = TerrainLightSnapshot.Capture(chunkValues.AsSpan(0, 65536), previous[i]);
            }

            return result;

            void Increase(int index, byte from)
            {
                var attenuation = loss[index];
                if (attenuation >= 15) { return; }

                var before = values[index];
                var sky = Math.Max(before >> 4, (from >> 4) - attenuation);
                var block = Math.Max(before & 15, (from & 15) - attenuation);
                var next = (byte)((sky << 4) | block);
                if (next == before) { return; }

                values[index] = next;
                queue.Enqueue(index);
            }
        }
        finally
        {
            pool.Return(values);
            pool.Return(loss);
            pool.Return(chunkValues);
            pool.Return(chunkLoss);
        }
    }
}
