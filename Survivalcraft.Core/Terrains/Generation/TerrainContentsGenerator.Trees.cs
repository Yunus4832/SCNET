using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using EntitySystem.TemplatesDatabase;

namespace Game.Terrains.Generation;

public partial class TerrainContentsGenerator
{
    private readonly ConditionalWeakTable<TerrainChunk, PreparedTrees> _preparedTrees = new();

    private sealed class PreparedTrees(ulong generation, BrushPaint[][] brushes)
    {
        public ulong Generation { get; } = generation;

        public BrushPaint[][] Brushes { get; } = brushes;
    }


    private readonly ConcurrentDictionary<(int Seed, Point2 Coords), CleanTreeBrushCacheEntry> _cleanTreeBrushCache =
        new();

    private long _cleanTreeBrushCleanupTicks;

    private long _cleanTreeBrushRequestCount;

    private const int _cleanTreeBrushCacheCleanupInterval = 256;

    private const int _cleanTreeBrushCacheMaxEntries = 4096;

    private const long _cleanTreeBrushCacheMaxAgeMs = 10 * 60 * 1000;

    public void GenerateLogs(TerrainChunk chunk)
    {
        if (!tgExtras)
        {
            return;
        }

        var x = chunk.Origin.X;
        var chunkEndX = x + 16;
        var y = chunk.Origin.Y;
        var chunkEndZ = y + 16;
        var x2 = chunk.Coords.X;
        var y2 = chunk.Coords.Y;
        for (var i = x2; i <= x2; i++)
        {
            for (var j = y2; j <= y2; j++)
            {
                var random = new Random(seed + i + 3943 * j);
                var humidity = CalculateHumidity(i * 16, j * 16);
                var temperature = CalculateTemperature(i * 16, j * 16);
                var logDensity = MathUtils.Saturate(
                    (SimplexNoise.OctavedNoise(i, j, 0.1f, 2, 2f, 0.5f) - 0.25f) / 0.2f +
                    (random.Bool(0.25f) ? 0.5f : 0f)
                );
                var targetCount = 0;
                if (logDensity > 0.9f)
                {
                    targetCount = random.Int(1, 2);
                }
                else if (logDensity > 0.5f)
                {
                    targetCount = random.Int(0, 1);
                }

                var placedCount = 0;
                for (var k = 0; k < 16; k++)
                {
                    if (placedCount >= targetCount)
                    {
                        break;
                    }

                    var startX = i * 16 + random.Int(0, 15);
                    var startZ = j * 16 + random.Int(0, 15);
                    var placementY = _terrain.CalculateTopmostCellHeight(startX, startZ);
                    if (placementY < 66)
                    {
                        continue;
                    }

                    var cellContentsFast = _terrain.GetCellContentsFast(startX, placementY, startZ);
                    if (cellContentsFast != 2 && cellContentsFast != 8)
                    {
                        continue;
                    }

                    placementY++;
                    var logLength = random.Int(3, 7);
                    var point = CellFace.FaceToPoint3(random.Int(0, 3));
                    if (point.X < 0 && startX - logLength + 1 < 0)
                    {
                        point.X *= -1;
                    }

                    if (point.X > 0 && startX + logLength - 1 > 15)
                    {
                        point.X *= -1;
                    }

                    if (point.Z < 0 && startZ - logLength + 1 < 0)
                    {
                        point.Z *= -1;
                    }

                    if (point.Z > 0 && startZ + logLength - 1 > 15)
                    {
                        point.Z *= -1;
                    }

                    var pathClear = true;
                    var firstHalfSupported = false;
                    var secondHalfSupported = false;
                    for (var l = 0; l < logLength; l++)
                    {
                        var testX = startX + point.X * l;
                        var testZ = startZ + point.Z * l;
                        if (testX < x + 1 ||
                            testX >= chunkEndX - 1 ||
                            testZ < y + 1 ||
                            testZ >= chunkEndZ - 1 ||
                            _subsystemTerrain.Catalog
                                .Blocks[_terrain.GetCellContentsFast(testX, placementY, testZ)].Collidable)
                        {
                            pathClear = false;
                            break;
                        }

                        if (!_subsystemTerrain.Catalog
                                .Blocks[_terrain.GetCellContentsFast(testX, placementY - 1, testZ)].Collidable)
                        {
                            continue;
                        }

                        if (l <= MathUtils.Max(logLength / 2, 0))
                        {
                            firstHalfSupported = true;
                        }

                        if (l >= MathUtils.Min(logLength / 2 + 1, logLength - 1))
                        {
                            secondHalfSupported = true;
                        }
                    }

                    if (!(pathClear && firstHalfSupported && secondHalfSupported))
                    {
                        continue;
                    }

                    var point2 = point.X != 0 ? new Point3(0, 0, 1) : new Point3(1, 0, 0);
                    var treeType = PlantsManager.GenerateRandomTreeType(
                        random,
                        temperature + SubsystemWeather.GetTemperatureAdjustmentAtHeight(placementY),
                        humidity,
                        placementY,
                        2f
                    );
                    if (treeType.HasValue)
                    {
                        var treeTrunkValue = PlantsManager.GetTreeTrunkValue(treeType.Value);
                        treeTrunkValue = Terrain.ReplaceData(
                            treeTrunkValue,
                            WoodBlock.SetCutFace(Terrain.ExtractData(treeTrunkValue), point.X != 0 ? 1 : 0)
                        );
                        var treeLeavesValue = PlantsManager.GetTreeLeavesValue(treeType.Value);
                        for (var m = 0; m < logLength; m++)
                        {
                            var logX = startX + point.X * m;
                            var logZ = startZ + point.Z * m;
                            _terrain.SetCellValueFast(logX, placementY, logZ, treeTrunkValue);
                            if (m <= logLength / 2)
                            {
                                continue;
                            }

                            if (random.Bool(0.5f) &&
                                !_subsystemTerrain.Catalog
                                    .Blocks[_terrain.GetCellContentsFast(logX + point2.X, placementY, logZ + point2.Z)]
                                    .Collidable)
                            {
                                _terrain.SetCellValueFast(
                                    logX + point2.X,
                                    placementY,
                                    logZ + point2.Z,
                                    treeLeavesValue
                                );
                            }

                            if (random.Bool(0.05f) &&
                                !_subsystemTerrain.Catalog
                                    .Blocks[_terrain.GetCellContentsFast(logX + point2.X, placementY, logZ + point2.Z)]
                                    .Collidable)
                            {
                                _terrain.SetCellValueFast(logX + point2.X, placementY, logZ + point2.Z, treeTrunkValue);
                            }

                            if (random.Bool(0.5f) &&
                                !_subsystemTerrain.Catalog
                                    .Blocks[_terrain.GetCellContentsFast(logX - point2.X, placementY, logZ - point2.Z)]
                                    .Collidable)
                            {
                                _terrain.SetCellValueFast(
                                    logX - point2.X,
                                    placementY,
                                    logZ - point2.Z,
                                    treeLeavesValue
                                );
                            }

                            if (random.Bool(0.05f) &&
                                !_subsystemTerrain.Catalog
                                    .Blocks[_terrain.GetCellContentsFast(logX - point2.X, placementY, logZ - point2.Z)]
                                    .Collidable)
                            {
                                _terrain.SetCellValueFast(logX - point2.X, placementY, logZ - point2.Z, treeTrunkValue);
                            }

                            if (random.Bool(0.5f) &&
                                !_subsystemTerrain.Catalog
                                    .Blocks[_terrain.GetCellContentsFast(logX, placementY + 1, logZ)]
                                    .Collidable)
                            {
                                _terrain.SetCellValueFast(logX, placementY + 1, logZ, treeLeavesValue);
                            }
                        }
                    }

                    placedCount++;
                }
            }
        }
    }

    public void GenerateTrees(TerrainChunk chunk)
    {
        if (!tgExtras) { return; }

        var sources = RequirePreparedTrees(chunk).Brushes;
        foreach (var brushes in sources)
        {
            foreach (var paint in brushes)
            {
                paint.Brush.PaintFast(chunk, paint.Position.X, paint.Position.Y, paint.Position.Z);
            }
        }
    }

    private PreparedTrees RequirePreparedTrees(TerrainChunk chunk)
    {
        if (_preparedTrees.TryGetValue(chunk, out var prepared) && prepared.Generation == chunk.AllocationGeneration)
        {
            return prepared;
        }

        throw new InvalidOperationException(
            "Tree generation requires completed preparation for the current allocation."
        );
    }

    public Task PrepareChunkContentsPass4Async(TerrainChunk chunk, CancellationToken cancellationToken)
    {
        if (!tgExtras) { return Task.CompletedTask; }

        _preparedTrees.Remove(chunk);
        RegisterCleanTreeBrush(chunk);
        var brushes = new BrushPaint[9][];
        var prepared = new PreparedTrees(chunk.AllocationGeneration, brushes);
        var missing = new List<(int Index, Point2 Coords)>();
        var index = 0;
        for (var x = chunk.Coords.X - 1; x <= chunk.Coords.X + 1; x++)
        {
            for (var z = chunk.Coords.Y - 1; z <= chunk.Coords.Y + 1; z++)
            {
                var coords = new Point2(x, z);
                if (_cleanTreeBrushCache.TryGetValue((seed, coords), out var entry))
                {
                    entry.LastAccessTicks = Environment.TickCount64;
                    brushes[index] = entry.BrushPaints;
                }
                else { missing.Add((index, coords)); }

                index++;
            }
        }

        // Retain this batch's brushes independently of cache eviction or an application pause.
        for (var i = 0; i < brushes.Length; i++) { MaybeCleanupCleanTreeBrushCache(); }

        if (missing.Count == 0)
        {
            _preparedTrees.Add(chunk, prepared);
            return Task.CompletedTask;
        }

        var values = new ValuesDictionary();
        worldSettings.Save(values, false);
        var settings = new WorldSettings();
        settings.Load(values);
        return Task.Run(() =>
            {
                using var terrain = new Terrain();
                var source = new TerrainContentsGenerator(this, terrain, settings);
                Parallel.ForEach(
                    missing,
                    new ParallelOptions
                    {
                        CancellationToken = cancellationToken,
                        MaxDegreeOfParallelism = SeedTerrainGenerationPolicy.GetParallelism(Environment.ProcessorCount)
                    },
                    item => brushes[item.Index] = GetCleanTreeBrush(item.Coords, source)
                );
                _preparedTrees.Add(chunk, prepared);
            }
        );
    }

    private BrushPaint[] GetCleanTreeBrush(Point2 coords, TerrainContentsGenerator source)
    {
        var entry = _cleanTreeBrushCache.GetOrAdd(
            (seed, coords),
            static (key, generator) => generator.CreateCleanTreeBrushEntry(key.Coords),
            source
        );
        entry.LastAccessTicks = Environment.TickCount64;
        MaybeCleanupCleanTreeBrushCache();
        return entry.BrushPaints;
    }

    private CleanTreeBrushCacheEntry CreateCleanTreeBrushEntry(Point2 coords)
    {
        var sourceChunk = CreateTreeBrushSourceChunk(coords);
        var brushPaints = GenerateCleanTreeBrush(sourceChunk);
        var basis = new SeedGeneratedChunkBasis(sourceChunk.Cells, sourceChunk.Shafts);
        sourceChunk.Dispose();
        return new CleanTreeBrushCacheEntry(brushPaints, Environment.TickCount64, basis);
    }

    private void RegisterCleanTreeBrush(TerrainChunk chunk)
    {
        _cleanTreeBrushCache.GetOrAdd(
            (seed, chunk.Coords),
            static (_, state) =>
                new CleanTreeBrushCacheEntry(
                    state.Generator.GenerateCleanTreeBrush(state.Chunk),
                    Environment.TickCount64,
                    null
                ),
            (Generator: this, Chunk: chunk)
        );
    }

    public bool TryTakeSeedGeneratedChunkBasis(TerrainChunk chunk)
    {
        return _cleanTreeBrushCache.TryGetValue((seed, chunk.Coords), out var entry) &&
               entry.Basis?.TryMoveTo(chunk) == true;
    }

    private TerrainChunk CreateTreeBrushSourceChunk(Point2 coords)
    {
        var chunk = new TerrainChunk(_terrain, coords.X, coords.Y);
        GenerateChunkContentsPass1(chunk);
        GenerateChunkContentsPass2(chunk);
        GenerateChunkContentsPass3(chunk);
        return chunk;
    }

    private BrushPaint[] GenerateCleanTreeBrush(TerrainChunk chunk)
    {
        var result = new List<BrushPaint>();
        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        for (var i = x; i <= x; i++)
        {
            for (var j = y; j <= y; j++)
            {
                var random = new Random(seed + i + 3943 * j);
                var humidity = CalculateHumidity(i * 16, j * 16);
                var temperature = CalculateTemperature(i * 16, j * 16);
                var forestDensity = CalculateForestDensity(i * 16, j * 16);
                var treeLimit = (int)(6f * forestDensity);
                var treeCount = 0;
                for (var k = 0; k < 36; k++)
                {
                    if (treeCount >= treeLimit)
                    {
                        break;
                    }

                    var randomX = i * 16 + random.Int(2, 13);
                    var randomZ = j * 16 + random.Int(2, 13);
                    var localX = randomX & 0xF;
                    var localZ = randomZ & 0xF;
                    var heightLimit = chunk.CalculateTopmostCellHeight(localX, localZ);
                    if (heightLimit < 66)
                    {
                        continue;
                    }

                    var cellContentsFast = chunk.GetCellContentsFast(localX, heightLimit, localZ);
                    if (cellContentsFast != 2 && cellContentsFast != 8)
                    {
                        continue;
                    }

                    heightLimit++;

                    if (_generationBlocks[chunk.GetCellContentsFast(localX + 1, heightLimit, localZ)].Collidable ||
                        _generationBlocks[chunk.GetCellContentsFast(localX - 1, heightLimit, localZ)].Collidable ||
                        _generationBlocks[chunk.GetCellContentsFast(localX, heightLimit, localZ + 1)].Collidable ||
                        _generationBlocks[chunk.GetCellContentsFast(localX, heightLimit, localZ - 1)].Collidable)
                    {
                        continue;
                    }

                    var treeType = PlantsManager.GenerateRandomTreeType(
                        random,
                        temperature + SubsystemWeather.GetTemperatureAdjustmentAtHeight(heightLimit),
                        humidity,
                        heightLimit
                    );
                    if (treeType.HasValue)
                    {
                        var treeBrushes = PlantsManager.GetTreeBrushes(treeType.Value);
                        var treeBrush = treeBrushes[random.Int(treeBrushes.Count)];
                        result.Add(
                            new BrushPaint
                            {
                                Position = new Point3(randomX, heightLimit, randomZ),
                                Brush = treeBrush
                            }
                        );
                    }

                    treeCount++;
                }
            }
        }

        return result.ToArray();
    }

    private void MaybeCleanupCleanTreeBrushCache()
    {
        if ((Interlocked.Increment(ref _cleanTreeBrushRequestCount) & (_cleanTreeBrushCacheCleanupInterval - 1)) != 0)
        {
            return;
        }

        var now = Environment.TickCount64;
        var previousCleanupTicks = Interlocked.Read(ref _cleanTreeBrushCleanupTicks);
        if (now - previousCleanupTicks < 5000)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _cleanTreeBrushCleanupTicks, now, previousCleanupTicks) !=
            previousCleanupTicks)
        {
            return;
        }

        foreach (var (key, entry) in _cleanTreeBrushCache)
        {
            if (now - entry.LastAccessTicks > _cleanTreeBrushCacheMaxAgeMs)
            {
                _cleanTreeBrushCache.TryRemove(key, out _);
            }
        }

        var overflow = _cleanTreeBrushCache.Count - _cleanTreeBrushCacheMaxEntries;
        if (overflow <= 0)
        {
            return;
        }

        var oldestEntries = _cleanTreeBrushCache
            .OrderBy(pair => pair.Value.LastAccessTicks)
            .Take(overflow)
            .ToArray();

        foreach (var (key, _) in oldestEntries)
        {
            _cleanTreeBrushCache.TryRemove(key, out _);
        }
    }

    private sealed class CleanTreeBrushCacheEntry(
        BrushPaint[] brushPaints,
        long lastAccessTicks,
        SeedGeneratedChunkBasis? basis
    )
    {
        public readonly BrushPaint[] BrushPaints = brushPaints;

        public readonly SeedGeneratedChunkBasis? Basis = basis;

        public long LastAccessTicks = lastAccessTicks;
    }
}
