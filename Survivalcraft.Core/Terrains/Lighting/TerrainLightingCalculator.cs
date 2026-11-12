using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace Game.Terrains.Lighting;

/// <summary>Captures independent sky/emission inputs and publishes private chunk propagation results.</summary>
public sealed class TerrainLightingCalculator(
    Terrain terrain,
    BlockContentCatalog catalog,
    bool includePredictions,
    Func<TerrainChunk, bool> canReadNeighbor
)
{
    public bool CalculateHeights(TerrainChunk chunk)
    {
        return ApplyHeights(chunk, catalog, includePredictions);
    }

    public TerrainLightPublication CalculateLightSeed(TerrainChunk chunk)
    {
        var input = terrain.Lighting.CaptureInput(chunk);
        terrain.Lighting.TryGetPropagationSnapshot(chunk, out var previousInput);
        var values = ArrayPool<byte>.Shared.Rent(65536);
        byte[]? transmission = null;
        try
        {
            transmission = ArrayPool<byte>.Shared.Rent(65536);

            for (var x = 0; x < 16; x++)
            {
                for (var z = 0; z < 16; z++)
                {
                    var sky = 15;
                    for (var y = 255; y >= 0; y--)
                    {
                        var index = TerrainChunk.CalculateCellIndex(x, y, z);
                        var value = GetCellValue(chunk, index, includePredictions);
                        var block = catalog.Blocks[Terrain.ExtractContents(value)];
                        transmission[index] = block.Transparent
                            ? (byte)Math.Clamp(block.LightAttenuation + 1, 1, 255)
                            : byte.MaxValue;
                        if (!block.Transparent || block.LightAttenuation >= sky) { sky = 0; }
                        else { sky -= block.LightAttenuation; }

                        var emitted = block.EmittedLightAmount > 0 ? block.GetEmittedLightAmount(value) : 0;
                        values[index] = new TerrainLightChannels(sky, emitted).Packed;
                    }
                }
            }

            if (!terrain.Lighting.TryPublishSeed(
                    chunk,
                    input,
                    new TerrainLightSeed(
                        TerrainLightSnapshot.Capture(values.AsSpan(0, 65536)),
                        transmission.AsSpan(0, 65536)
                    )
                )) { return default; }

            if (!terrain.Lighting.TryGetPropagationSnapshot(chunk, out var currentInput)) { return default; }

            var faces = ChangedFaces(previousInput, currentInput);
            return new TerrainLightPublication(true, faces != 0, faces);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(values);
            if (transmission != null) { ArrayPool<byte>.Shared.Return(transmission); }
        }
    }

    public void CalculateHeightsAndLightSeed(TerrainChunk chunk)
    {
        CalculateHeights(chunk);
        CalculateLightSeed(chunk);
    }

    public bool TryCapturePropagation(TerrainChunk chunk, [NotNullWhen(true)] out TerrainLightJob? job)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        job = null;
        if (chunk.IsDisposed) { return false; }

        if (!ReferenceEquals(chunk.Terrain, terrain))
        {
            throw new ArgumentException("The light task requires its owning terrain.", nameof(chunk));
        }

        var input = terrain.Lighting.CaptureInput(chunk);
        if (!terrain.Lighting.TryGetSeed(chunk, out var seed)) { return false; }

        var neighbors = new TerrainLightSnapshot?[4];
        for (var face = 0; face < neighbors.Length; face++)
        {
            var neighbor = Neighbor(chunk, face);
            if (neighbor != null && canReadNeighbor(neighbor))
            {
                terrain.Lighting.TryGetPropagationSnapshot(neighbor, out neighbors[face]);
            }
        }

        if (chunk.IsDisposed || terrain.Lighting.CaptureInput(chunk) != input) { return false; }

        job = new TerrainLightJob(chunk, input, seed, neighbors);
        return true;
    }

    public TerrainLightPublication Publish(TerrainLightJob job, TerrainLightSnapshot result)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(result);
        var chunk = job.Chunk;
        if (!ReferenceEquals(chunk.Terrain, terrain))
        {
            throw new ArgumentException("The result belongs to its captured world.", nameof(job));
        }

        if (chunk.IsDisposed || terrain.Lighting.CaptureInput(chunk) != job.Input || !BoundaryInputsCurrent(job))
        {
            return default;
        }

        terrain.Lighting.TryGetPublishedSnapshot(chunk, out var previous);
        terrain.Lighting.TryGetPropagationSnapshot(chunk, out var previousInput);
        if (!terrain.Lighting.TryPublish(chunk, job.Input, result)) { return default; }

        return new TerrainLightPublication(
            true,
            previous == null || !previous.HasSameValues(result),
            ChangedFaces(previousInput, result)
        );
    }

    private static int ChangedFaces(TerrainLightSnapshot? previous, TerrainLightSnapshot current)
    {
        var faces = 0;
        previous ??= new TerrainLightSnapshot(default);
        for (var face = 0; face < 4; face++)
        {
            if (!previous.HasSameBoundary(current, face)) { faces |= 1 << face; }
        }

        return faces;
    }

    public TerrainLightPublication Propagate(TerrainChunk chunk)
    {
        return TryCapturePropagation(chunk, out var job) ? Publish(job, job.Compute()) : default;
    }

    public static void CalculateHeightsAndLightSeed(
        TerrainChunk chunk,
        BlockContentCatalog catalog,
        bool includePredictions = false
    )
    {
        new TerrainLightingCalculator(chunk.Terrain, catalog, includePredictions, static _ => true)
            .CalculateHeightsAndLightSeed(chunk);
    }

    private bool BoundaryInputsCurrent(TerrainLightJob task)
    {
        for (var face = 0; face < 4; face++)
        {
            var neighbor = Neighbor(task.Chunk, face);
            TerrainLightSnapshot? current = null;
            if (neighbor != null && canReadNeighbor(neighbor))
            {
                terrain.Lighting.TryGetPropagationSnapshot(neighbor, out current);
            }

            var captured = task.Neighbor(face);
            if (ReferenceEquals(current, captured)) { continue; }

            current ??= new TerrainLightSnapshot(default);
            captured ??= new TerrainLightSnapshot(default);
            if (!current.HasSameBoundary(captured, (face + 2) & 3)) { return false; }
        }

        return true;
    }

    private TerrainChunk? Neighbor(TerrainChunk chunk, int face)
    {
        var x = chunk.Coords.X + (face == 1 ? 1 : face == 3 ? -1 : 0);
        var z = chunk.Coords.Y + (face == 0 ? 1 : face == 2 ? -1 : 0);
        return terrain.GetChunkAtCoords(x, z);
    }

    private static int GetCellValue(TerrainChunk chunk, int index, bool includePredictions)
    {
        return includePredictions ? chunk.GetCellValueForGeometry(index) : chunk.GetCellValueFast(index);
    }

    private static bool ApplyHeights(TerrainChunk chunk, BlockContentCatalog catalog, bool includePredictions)
    {
        var changed = false;
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                var topHeight = 0;
                var bottomHeight = 256;
                var y = 255;
                var index = TerrainChunk.CalculateCellIndex(x, y, z);
                while (y >= 0)
                {
                    if (Terrain.ExtractContents(GetCellValue(chunk, index, includePredictions)) != 0)
                    {
                        topHeight = y;
                        break;
                    }

                    y--;
                    index--;
                }

                y = 0;
                index = TerrainChunk.CalculateCellIndex(x, y, z);
                while (y <= topHeight + 1)
                {
                    var contents = Terrain.ExtractContents(GetCellValue(chunk, index, includePredictions));
                    if (catalog.Blocks[contents].Transparent)
                    {
                        bottomHeight = y;
                        break;
                    }

                    y++;
                    index++;
                }

                changed |= chunk.GetTopHeightFast(x, z) != topHeight ||
                           chunk.GetBottomHeightFast(x, z) != bottomHeight;
                chunk.SetTopHeightFast(x, z, topHeight);
                chunk.SetBottomHeightFast(x, z, bottomHeight);
            }
        }

        return changed;
    }
}
