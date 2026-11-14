using System.Runtime.CompilerServices;

using Engine.Graphics;

using Game.Blocks.Presentation.Geometry;

namespace Game.Terrains.Rendering;

/// <summary>CPU-only slice-cache builder; uploads remain on the rendering thread.</summary>
internal sealed class TerrainMeshBuilder(
    Terrain terrain,
    BlockGeometryGenerator generator,
    Texture2D texture,
    TimeProvider? timeProvider = null
)
{
    private readonly Terrain _terrain = terrain;

    private readonly BlockGeometryGenerator _generator = generator;

    private readonly Texture2D _texture = texture;

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    private readonly Game.Terrains.Lighting.TerrainLightSnapshot?[] _hashLights =
        new Game.Terrains.Lighting.TerrainLightSnapshot?[9];

    private readonly TerrainChunk?[] _neighbors = new TerrainChunk?[9];

    internal bool GenerateSlice(
        TerrainChunk chunk,
        TerrainChunkPresentation output,
        int sliceIndex,
        TimeSpan maximumTime
    )
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)sliceIndex, (uint)TerrainChunk.SlicesCount);
        if (maximumTime <= TimeSpan.Zero) { return false; }

        var started = _time.GetTimestamp();
        _generator.ResetCache();
        if (output.BuildLights is { } lights) { _generator.SetLightSnapshots(chunk.Coords, lights); }

        if (!output.Draws.TryGetValue(_texture, out var terrainGeometry))
        {
            terrainGeometry = new TerrainGeometry[TerrainChunk.SlicesCount];
            for (var i = 0; i < TerrainChunk.SlicesCount; i++)
            {
                terrainGeometry[i] = new TerrainGeometry(output.Draws, i);
            }

            output.Draws.Add(_texture, terrainGeometry);
        }

        var work = output.ActiveSlice;
        if (work == null)
        {
            var hash = CalculateChunkSliceContentsHash(chunk, sliceIndex, output.BuildLights);
            if (!output.BeginSliceGeometryUpdate(sliceIndex, hash)) { return true; }

            foreach (var draw in output.Draws)
            {
                foreach (var subset in draw.Value[sliceIndex].Subsets)
                {
                    subset.Vertices.Clear();
                    subset.Indices.Clear();
                }
            }

            work = new TerrainChunkPresentation.SliceWork(sliceIndex, hash);
            output.ActiveSlice = work;
        }

        else { CaptureInputs(chunk, output.BuildLights); }

        if (work.Slice != sliceIndex)
        {
            throw new InvalidOperationException("Terrain slice cursor does not match the active build.");
        }

        try
        {
            var hadColumns = work.Column < 16 * 16;
            while (work.Column < 16 * 16)
            {
                var x = work.Column / 16;
                var z = work.Column % 16;
                if (ColumnInputsAvailable(x, z))
                {
                    GenerateColumn(chunk, terrainGeometry[sliceIndex], x, z, sliceIndex);
                }

                work.Column++;
                if (work.Column < 16 * 16 && _time.GetElapsedTime(started) >= maximumTime) { return false; }
            }

            if (hadColumns && _time.GetElapsedTime(started) >= maximumTime) { return false; }

            if (!CompactSlice(output, work, started, maximumTime)) { return false; }

            output.CompleteSliceGeometryUpdate(sliceIndex, work.ContentsHash);
            output.ActiveSlice = null;
            return true;
        }
        catch
        {
            // A failed generator may already have appended vertices. Retry the entire invalid slice.
            output.ActiveSlice = null;
            throw;
        }
    }

    private bool CompactSlice(
        TerrainChunkPresentation output,
        TerrainChunkPresentation.SliceWork work,
        long started,
        TimeSpan maximumTime
    )
    {
        var index = 0;
        foreach (var slices in output.Draws.Values)
        {
            foreach (var subset in slices[work.Slice].Subsets)
            {
                if (index++ >= work.CompactionBuffer)
                {
                    var changed = TrimBufferCapacity(subset.Vertices);
                    work.CompactionBuffer++;
                    if (changed && _time.GetElapsedTime(started) >= maximumTime) { return false; }
                }

                if (index++ >= work.CompactionBuffer)
                {
                    var changed = TrimBufferCapacity(subset.Indices);
                    work.CompactionBuffer++;
                    if (changed && _time.GetElapsedTime(started) >= maximumTime) { return false; }
                }
            }
        }

        return true;
    }

    internal static bool TrimBufferCapacity<T>(DynamicArray<T> buffer) where T : unmanaged
    {
        // Keep 25% growth room. The 50% trigger and byte threshold avoid resizing for minor changes.
        if ((long)buffer.Capacity - buffer.Count < buffer.Count / 2) { return false; }

        var retained = buffer.Count == 0
            ? 0
            : Math.Min(buffer.Capacity, (long)buffer.Count + Math.Max(4, buffer.Count / 4));
        if ((buffer.Capacity - retained) * Unsafe.SizeOf<T>() < 4096) { return false; }

        buffer.Capacity = (int)retained;
        return true;
    }

    private bool ColumnInputsAvailable(int x, int z)
    {
        var dx = x == 0 ? -1 : x == 15 ? 1 : 0;
        var dz = z == 0 ? -1 : z == 15 ? 1 : 0;
        return (dx == 0 || _neighbors[(dx + 1) * 3 + 1] != null) &&
               (dz == 0 || _neighbors[3 + dz + 1] != null) &&
               (dx == 0 || dz == 0 || _neighbors[(dx + 1) * 3 + dz + 1] != null);
    }

    private void GenerateColumn(TerrainChunk chunk, TerrainGeometry geometry, int x, int z, int slice)
    {
        var worldX = x + chunk.Origin.X;
        var worldZ = z + chunk.Origin.Y;
        var bottom = MathUtils.Min(
            chunk.GetBottomHeightFast(x, z) - 1,
            MathUtils.Min(
                _terrain.GetBottomHeight(worldX - 1, worldZ),
                _terrain.GetBottomHeight(worldX + 1, worldZ),
                _terrain.GetBottomHeight(worldX, worldZ - 1),
                _terrain.GetBottomHeight(worldX, worldZ + 1)
            )
        );
        var first = MathUtils.Max(16 * slice, bottom, 1);
        var last = MathUtils.Min(16 * (slice + 1), chunk.GetTopHeightFast(x, z) + 1, 256);
        var index = TerrainChunk.CalculateCellIndex(x, 0, z);
        for (var y = first; y < last; y++)
        {
            var value = chunk.GetCellValueForGeometry(index + y);
            var contents = Terrain.ExtractContents(value);
            if (contents == 0) { continue; }

            _generator.Presentations.Get(contents)
                .GenerateTerrainVertices(_generator, geometry, value, worldX, y, worldZ);
        }
    }

    private void CaptureInputs(TerrainChunk chunk, Game.Terrains.Lighting.TerrainLightSnapshot?[]? lights = null)
    {
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                var neighbor = _terrain.GetChunkAtCoords(chunk.Coords.X + dx, chunk.Coords.Y + dz);
                _neighbors[(dx + 1) * 3 + dz + 1] = neighbor;
                Game.Terrains.Lighting.TerrainLightSnapshot? snapshot = null;
                if (lights != null) { snapshot = lights[(dx + 1) * 3 + dz + 1]; }
                else if (neighbor != null) { _terrain.Lighting.TryGetPublishedSnapshot(neighbor, out snapshot); }

                _hashLights[(dx + 1) * 3 + dz + 1] = snapshot;
            }
        }
    }

    internal int CalculateChunkSliceContentsHash(
        TerrainChunk chunk,
        int sliceIndex,
        Game.Terrains.Lighting.TerrainLightSnapshot?[]? lights = null
    )
    {
        CaptureInputs(chunk, lights);
        var num = 1;
        var num2 = chunk.Origin.X - 1;
        var num3 = chunk.Origin.X + 16 + 1;
        var num4 = chunk.Origin.Y - 1;
        var num5 = chunk.Origin.Y + 16 + 1;
        var x = MathUtils.Max(16 * sliceIndex - 1, 0);
        var x2 = MathUtils.Min(16 * (sliceIndex + 1) + 1, 256);
        for (var i = num2; i < num3; i++)
        {
            for (var j = num4; j < num5; j++)
            {
                var chunkAtCell = _terrain.GetChunkAtCell(i, j, false);
                if (chunkAtCell == null)
                {
                    continue;
                }

                var light = _hashLights[(chunkAtCell.Coords.X - chunk.Coords.X + 1) * 3 +
                                        chunkAtCell.Coords.Y -
                                        chunk.Coords.Y +
                                        1];
                var x3 = i & 0xF;
                var z = j & 0xF;
                var shaftValueFast = chunkAtCell.GetShaftValueFast(x3, z);
                var num6 = Terrain.ExtractBottomHeight(shaftValueFast);
                var num7 = Terrain.ExtractTopHeight(shaftValueFast);
                var num8 = MathUtils.Max(x, num6 - 1);
                var num9 = MathUtils.Min(x2, num7 + 2);
                var num10 = TerrainChunk.CalculateCellIndex(x3, num8, z);
                var num11 = num10 + num9 - num8;
                while (num10 < num11)
                {
                    num += chunkAtCell.GetCellValueForGeometry(num10);
                    num *= 31;
                    num += light?.Get(x3, num10 & 255, z).Packed ?? 0;
                    num *= 31;
                    num10++;
                }

                num += Terrain.ExtractTemperature(shaftValueFast);
                num *= 31;
                num += Terrain.ExtractHumidity(shaftValueFast);
                num *= 31;
                num += num8;
                num *= 31;
            }
        }

        num += _terrain.SeasonTemperature;
        num *= 31;
        num += _terrain.SeasonHumidity;
        num *= 31;
        return num;
    }
}
