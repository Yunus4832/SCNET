using System.Runtime.CompilerServices;

using Engine.Graphics;

using Game.Blocks.Presentation;

namespace Game.Terrains.Rendering;

/// <summary>仅在 CPU 上构建分片缓存，上传由渲染线程执行。</summary>
internal sealed class TerrainMeshBuilder(
    Terrain terrain,
    BlockGeometryGenerator generator,
    Texture2D texture,
    TimeProvider? timeProvider = null
)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    private readonly Lighting.TerrainLightSnapshot?[] _hashLights =
        new Lighting.TerrainLightSnapshot?[9];

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
        generator.ResetCache();
        generator.DisplayedEmissions = output.DisplayedEmissions(sliceIndex);
        if (output.BuildLights is { } lights) { generator.SetLightSnapshots(chunk.Coords, lights); }

        if (!output.Draws.TryGetValue(texture, out var terrainGeometry))
        {
            terrainGeometry = new TerrainGeometry[TerrainChunk.SlicesCount];
            for (var i = 0; i < TerrainChunk.SlicesCount; i++)
            {
                terrainGeometry[i] = new TerrainGeometry(output.Draws, i);
            }

            output.Draws.Add(texture, terrainGeometry);
        }

        var work = output.ActiveSlice;
        if (work == null)
        {
            if (output.TryReuseSliceGeometry(sliceIndex, out var inputs))
            {
                CaptureInputs(chunk, output.BuildLights);
            }
            else
            {
                inputs = CalculateSliceInputs(
                    chunk,
                    sliceIndex,
                    output.BuildLights,
                    output.LightReadsForSlice(sliceIndex),
                    generator.Presentations.ReplayableLightbulbIndex,
                    output
                );
            }

            if (output.MatchesSliceInputs(sliceIndex, inputs))
            {
                generator.DisplayedEmissions = null;
                return true;
            }

            var refreshLighting = output.BuildLights != null && output.CanRefreshSliceLighting(sliceIndex, inputs);
            var relight = refreshLighting ? FindRelightSubsets(output, sliceIndex) : null;
            output.BeginSliceGeometryUpdate(sliceIndex, inputs, geometryChanges: relight == null);

            if (relight == null)
            {
                foreach (var draw in output.Draws.Values)
                {
                    foreach (var subset in draw[sliceIndex].Subsets)
                    {
                        subset.ClearGeometry();
                    }
                }
            }

            work = new TerrainChunkPresentation.SliceWork(sliceIndex, inputs) { RelightSubsets = relight };
            output.ActiveSlice = work;
        }

        else { CaptureInputs(chunk, output.BuildLights); }

        if (work.Slice != sliceIndex)
        {
            throw new InvalidOperationException("Terrain slice cursor does not match the active build.");
        }

        generator.SetGeometryNeighborhood(chunk.Coords, _neighbors);
        generator.SetLightReadTarget(output.BuildLights != null ? work : null);
        try
        {
            if (work.RelightSubsets is { } subsets)
            {
                if (!RelightSlice(work, subsets, started, maximumTime)) { return false; }

                CompleteSlice(output, work);
                output.ActiveSlice = null;
                return true;
            }

            var hadColumns = work.Column < 16 * 16;
            while (work.Column < 16 * 16)
            {
                var x = work.Column / 16;
                var z = work.Column % 16;
                if (ColumnInputsAvailable(x, z))
                {
                    GenerateColumn(chunk, terrainGeometry[sliceIndex], x, z, work);
                }
                else
                {
                    GenerateUnresolvedWaterSurface(chunk, terrainGeometry[sliceIndex], x, z, work);
                }

                work.Column++;
                if (work.Column < 16 * 16 && _time.GetElapsedTime(started) >= maximumTime) { return false; }
            }

            if (hadColumns && _time.GetElapsedTime(started) >= maximumTime) { return false; }

            if (!CompactSlice(output, work, started, maximumTime)) { return false; }

            CompleteSlice(output, work);
            output.ActiveSlice = null;
            return true;
        }
        catch
        {
            // A failed generator may already have appended vertices. Retry the entire invalid slice.
            output.ActiveSlice = null;
            throw;
        }
        finally
        {
            generator.SetGeometryNeighborhood(default, null);
            generator.SetLightReadTarget(null);
            generator.DisplayedEmissions = null;
        }
    }

    private void CompleteSlice(TerrainChunkPresentation output, TerrainChunkPresentation.SliceWork work)
    {
        var inputs = work.Inputs;
        if (output.BuildLights != null)
        {
            output.MarkSliceLightReads(work.Slice, in work.LightReads);
            inputs = inputs with { Lighting = output.LightReadsForSlice(work.Slice).Fingerprint(_hashLights) };
        }

        output.CompleteSliceGeometryUpdate(work.Slice, inputs, work.CanReplayLighting);
    }

    private static TerrainGeometrySubset[]? FindRelightSubsets(TerrainChunkPresentation output, int slice)
    {
        var subsets = new List<TerrainGeometrySubset>();
        foreach (var slices in output.Draws.Values)
        {
            foreach (var subset in slices[slice].Subsets)
            {
                if (subset.Vertices.Count == 0) { continue; }

                if (!subset.HasCompleteLighting) { return null; }

                subsets.Add(subset);
            }
        }

        return subsets.ToArray();
    }

    private bool RelightSlice(
        TerrainChunkPresentation.SliceWork work,
        TerrainGeometrySubset[] subsets,
        long started,
        TimeSpan maximumTime
    )
    {
        while (work.LightSubsetIndex < subsets.Length)
        {
            var subset = subsets[work.LightSubsetIndex];
            while (work.LightRunIndex < subset.LightingRunCount)
            {
                if (_time.GetElapsedTime(started) >= maximumTime) { return false; }

                var run = subset.GetLightingRun(work.LightRunIndex);
                var end = Math.Min(work.LightVertexIndex + 64, run.End);
                generator.RefreshLighting(subset, run, work.LightVertexIndex, end);
                work.LightVertexIndex = end;
                if (end == run.End) { work.LightRunIndex++; }
            }

            work.LightSubsetIndex++;
            work.LightRunIndex = 0;
            work.LightVertexIndex = 0;
        }

        return true;
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

    // Keep the exposed water surface while a neighbor needed by the full column is still missing.
    private void GenerateUnresolvedWaterSurface(
        TerrainChunk chunk,
        TerrainGeometry geometry,
        int x,
        int z,
        TerrainChunkPresentation.SliceWork work
    )
    {
        var y = chunk.GetTopHeightFast(x, z);
        if (y < 1 || y >> 4 != work.Slice) { return; }

        var value = chunk.GetCellValueForGeometry(x, y, z);
        if (Terrain.ExtractContents(value) is var contents &&
            contents != 0 &&
            generator.Presentations.Get(contents) is WaterBlockPresentation water &&
            FluidBlock.GetIsTop(Terrain.ExtractData(value)))
        {
            work.CanReplayLighting &= water.SupportsTerrainLightingReplay;
            water.GenerateSurface(
                generator,
                geometry,
                value,
                chunk.Origin.X + x,
                y,
                chunk.Origin.Y + z,
                topOnly: true
            );
        }
    }

    private void GenerateColumn(
        TerrainChunk chunk,
        TerrainGeometry geometry,
        int x,
        int z,
        TerrainChunkPresentation.SliceWork work
    )
    {
        var slice = work.Slice;
        var worldX = x + chunk.Origin.X;
        var worldZ = z + chunk.Origin.Y;
        var bottom = MathUtils.Min(
            chunk.GetBottomHeightFast(x, z) - 1,
            MathUtils.Min(
                GetNeighborBottomHeight(x - 1, z),
                GetNeighborBottomHeight(x + 1, z),
                GetNeighborBottomHeight(x, z - 1),
                GetNeighborBottomHeight(x, z + 1)
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

            var presentation = generator.Presentations.Get(contents);
            // External generators may derive topology or vertex color from sampled light.
            work.CanReplayLighting &= presentation.SupportsTerrainLightingReplay;
            presentation.GenerateTerrainVertices(
                generator,
                geometry,
                generator.ApplyDisplayedEmission(value, worldX, y, worldZ),
                worldX,
                y,
                worldZ
            );
        }
    }

    private TerrainChunk? GetNeighborColumn(int x, int z)
    {
        return _neighbors[((x >> 4) + 1) * 3 + (z >> 4) + 1];
    }

    private int GetNeighborBottomHeight(int x, int z)
    {
        return GetNeighborColumn(x, z)?.GetBottomHeightFast(x & 15, z & 15) ?? 0;
    }

    private void CaptureInputs(TerrainChunk chunk, Game.Terrains.Lighting.TerrainLightSnapshot?[]? lights = null)
    {
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                var neighbor = terrain.GetChunkAtCoords(chunk.Coords.X + dx, chunk.Coords.Y + dz);
                _neighbors[(dx + 1) * 3 + dz + 1] = neighbor is { HasContents: true } ? neighbor : null;
                Game.Terrains.Lighting.TerrainLightSnapshot? snapshot = null;
                if (lights != null) { snapshot = lights[(dx + 1) * 3 + dz + 1]; }
                else if (neighbor != null) { terrain.Lighting.TryGetPublishedSnapshot(neighbor, out snapshot); }

                _hashLights[(dx + 1) * 3 + dz + 1] = snapshot;
            }
        }
    }

    internal TerrainSliceInputs CalculateSliceInputs(
        TerrainChunk chunk,
        int sliceIndex,
        Game.Terrains.Lighting.TerrainLightSnapshot?[]? lights = null,
        TerrainMeshLightReads? lightReads = null,
        int lightbulb = -1,
        TerrainChunkPresentation? output = null
    )
    {
        CaptureInputs(chunk, lights);
        var geometry = 1;
        Dictionary<Point3, int>? emissions = null;
        var emissiveMaterial = 0;
        var x = MathUtils.Max(16 * sliceIndex - 1, 0);
        var x2 = MathUtils.Min(16 * (sliceIndex + 1) + 1, 256);
        for (var i = -1; i <= 16; i++)
        {
            for (var j = -1; j <= 16; j++)
            {
                var chunkAtCell = GetNeighborColumn(i, j);
                if (chunkAtCell == null)
                {
                    geometry *= 31;
                    continue;
                }

                var x3 = i & 0xF;
                var z = j & 0xF;
                var shaftValueFast = chunkAtCell.GetShaftValueFast(x3, z);
                var bottomHeight = Terrain.ExtractBottomHeight(shaftValueFast);
                var topHeight = Terrain.ExtractTopHeight(shaftValueFast);
                var startY = MathUtils.Max(x, bottomHeight - 1);
                var endY = MathUtils.Min(x2, topHeight + 2);
                var startIndex = TerrainChunk.CalculateCellIndex(x3, startY, z);
                var endIndex = startIndex + endY - startY;
                while (startIndex < endIndex)
                {
                    var value = chunkAtCell.GetCellValueForGeometry(startIndex);
                    if (Terrain.ExtractContents(value) == lightbulb)
                    {
                        var pos = new Point3(chunk.Origin.X + i, startIndex & 255, chunk.Origin.Y + j);
                        if (output != null) { value = generator.ApplyDisplayedEmission(value, pos.X, pos.Y, pos.Z); }

                        var data = Terrain.ExtractData(value);
                        var intensity = LightbulbBlock.GetLightIntensity(data);
                        if (intensity > 0) { (emissions ??= [])[pos] = intensity; }

                        emissiveMaterial = 31 * emissiveMaterial + intensity;
                        value = Terrain.ReplaceData(value, LightbulbBlock.SetLightIntensity(data, 0));
                    }

                    geometry = 31 * geometry + value;
                    startIndex++;
                }

                geometry = 31 * geometry + Terrain.ExtractTemperature(shaftValueFast);
                geometry = 31 * geometry + Terrain.ExtractHumidity(shaftValueFast);
                geometry = 31 * geometry + startY;
                geometry = 31 * geometry + endY;
            }
        }

        output?.SetSliceEmissions(sliceIndex, emissions);
        geometry = 31 * geometry + terrain.SeasonTemperature;
        geometry = 31 * geometry + terrain.SeasonHumidity;
        return new TerrainSliceInputs(
            geometry,
            (lightReads ?? TerrainMeshLightReads.AroundSlice(sliceIndex)).Fingerprint(_hashLights),
            emissiveMaterial
        );
    }
}
