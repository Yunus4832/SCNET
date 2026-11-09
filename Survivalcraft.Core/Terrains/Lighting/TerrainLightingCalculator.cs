namespace Game.Terrains.Lighting;

/// <summary>Shared serial light calculations with explicit content inputs and no readiness policy.</summary>
public sealed class TerrainLightingCalculator(
    Terrain terrain,
    BlockContentCatalog catalog,
    bool includePredictions,
    Func<TerrainChunk, bool> canReadNeighbor
)
{
    private readonly Terrain _terrain = terrain;

    private readonly BlockContentCatalog _catalog = catalog;

    private readonly bool _includePredictions = includePredictions;

    private readonly Func<TerrainChunk, bool> _canReadNeighbor = canReadNeighbor;

    private readonly Queue<LightSource> _lightSources = new();

    private TerrainChunk? _propagationChunk;

    private ulong _propagationGeneration;

    public bool HasActivePropagation => _propagationChunk != null;

    private readonly HashSet<TerrainChunk> _changedChunks = [];

    /// <summary>Actual light/column changes in the last operation, consumed before the next call.</summary>
    public IReadOnlyCollection<TerrainChunk> ChangedChunks => _changedChunks;

    /// <summary>Updates only top/bottom column heights; light values and sunlight heights remain unchanged.</summary>
    public void CalculateHeights(TerrainChunk chunk)
    {
        _changedChunks.Clear();
        if (ApplyHeights(chunk, _catalog, _includePredictions)) { _changedChunks.Add(chunk); }
    }

    /// <summary>Initializes sunlight using column heights already calculated for the same content inputs.</summary>
    public void ResetSunlight(TerrainChunk chunk, int skylightValue)
    {
        _changedChunks.Clear();
        if (ApplySunlight(chunk, skylightValue, _catalog, _includePredictions)) { _changedChunks.Add(chunk); }
    }

    public void ResetSunlightAndHeight(TerrainChunk chunk, int skylightValue)
    {
        _changedChunks.Clear();
        var changed = ApplyHeights(chunk, _catalog, _includePredictions);
        changed |= ApplySunlight(chunk, skylightValue, _catalog, _includePredictions);
        if (changed) { _changedChunks.Add(chunk); }
    }

    /// <summary>Prepares one serial propagation task. ChangedChunks contains the preparation's actual writes.</summary>
    public void BeginPropagation(TerrainChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ObjectDisposedException.ThrowIf(chunk.IsDisposed, chunk);
        if (!ReferenceEquals(chunk.Terrain, _terrain))
        {
            throw new ArgumentException("Light propagation requires the owning terrain.", nameof(chunk));
        }

        if (HasActivePropagation)
        {
            throw new InvalidOperationException(
                "Cancel or finish the active light propagation before starting another."
            );
        }

        _changedChunks.Clear();
        _lightSources.Clear();
        _propagationChunk = chunk;
        _propagationGeneration = chunk.AllocationGeneration;
        try
        {
            GenerateChunkLightSources(chunk);
            GenerateChunkEdgeLightSources(chunk, 0);
            GenerateChunkEdgeLightSources(chunk, 1);
            GenerateChunkEdgeLightSources(chunk, 2);
            GenerateChunkEdgeLightSources(chunk, 3);
        }
        catch
        {
            ClearPropagation();
            throw;
        }
    }

    /// <summary>Processes a bounded FIFO batch; true means all initial and newly produced sources were consumed.</summary>
    public bool AdvancePropagation(TerrainChunk chunk, int maximumSources)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSources);
        RequireActivePropagation(chunk);
        _changedChunks.Clear();
        try
        {
            for (var processed = 0; processed < maximumSources && _lightSources.TryDequeue(out var source); processed++)
            {
                var light = source.Light;
                if (light <= 1) { continue; }

                PropagateLightSource(source.X - 1, source.Y, source.Z, light);
                PropagateLightSource(source.X + 1, source.Y, source.Z, light);
                if (source.Y > 0) { PropagateLightSource(source.X, source.Y - 1, source.Z, light); }

                if (source.Y < 255) { PropagateLightSource(source.X, source.Y + 1, source.Z, light); }

                PropagateLightSource(source.X, source.Y, source.Z - 1, light);
                PropagateLightSource(source.X, source.Y, source.Z + 1, light);
            }
        }
        catch
        {
            ClearPropagation();
            throw;
        }

        if (_lightSources.Count != 0) { return false; }

        ClearPropagation();
        return true;
    }

    /// <summary>Abandons pending sources without reverting light values already written.</summary>
    public void CancelPropagation()
    {
        _changedChunks.Clear();
        ClearPropagation();
    }

    /// <summary>Synchronous calculation convenience; production scheduling uses bounded AdvancePropagation batches.</summary>
    public void Propagate(TerrainChunk chunk)
    {
        BeginPropagation(chunk);
        var changed = new HashSet<TerrainChunk>(_changedChunks);
        bool complete;
        do
        {
            complete = AdvancePropagation(chunk, int.MaxValue);
            changed.UnionWith(_changedChunks);
        } while (!complete);

        _changedChunks.UnionWith(changed);
    }

    private void RequireActivePropagation(TerrainChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (_propagationChunk == null) { throw new InvalidOperationException("There is no active light propagation."); }

        if (!ReferenceEquals(_propagationChunk, chunk))
        {
            throw new ArgumentException("Light propagation belongs to another chunk.", nameof(chunk));
        }

        if (chunk.IsDisposed)
        {
            CancelPropagation();
            throw new ObjectDisposedException(nameof(TerrainChunk));
        }

        if (chunk.AllocationGeneration != _propagationGeneration)
        {
            CancelPropagation();
            throw new InvalidOperationException("The active light propagation allocation has expired.");
        }
    }

    private void ClearPropagation()
    {
        _lightSources.Clear();
        _propagationChunk = null;
        _propagationGeneration = 0;
    }

    public static void CalculateSunlightAndHeight(
        TerrainChunk chunk,
        int skylightValue,
        BlockContentCatalog catalog,
        bool includePredictions = false
    )
    {
        ApplyHeights(chunk, catalog, includePredictions);
        ApplySunlight(chunk, skylightValue, catalog, includePredictions);
    }

    private static int GetCellValue(TerrainChunk chunk, int index, bool includePredictions)
    {
        return includePredictions ? chunk.GetCellValueForGeometry(index) : chunk.GetCellValueFast(index);
    }

    private static bool SetCellLight(TerrainChunk chunk, int index, int light)
    {
        if (Terrain.ExtractLight(chunk.GetCellValueFast(index)) == light) { return false; }

        chunk.SetCellLightFast(index, light);
        return true;
    }

    private void SetTrackedLight(TerrainChunk chunk, int index, int light)
    {
        if (SetCellLight(chunk, index, light)) { _changedChunks.Add(chunk); }
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

    private static bool ApplySunlight(
        TerrainChunk chunk,
        int skylightValue,
        BlockContentCatalog catalog,
        bool includePredictions
    )
    {
        var changed = false;
        for (var i = 0; i < 16; i++)
        {
            for (var j = 0; j < 16; j++)
            {
                var num = chunk.GetTopHeightFast(i, j);
                var num2 = chunk.GetBottomHeightFast(i, j);
                var num4 = 255;
                var num5 = TerrainChunk.CalculateCellIndex(i, num4, j);
                while (num4 > num)
                {
                    changed |= SetCellLight(chunk, num5, skylightValue);
                    num4--;
                    num5--;
                }

                num4 = 0;
                num5 = TerrainChunk.CalculateCellIndex(i, num4, j);
                while (num4 < num2)
                {
                    changed |= SetCellLight(chunk, num5, 0);
                    num4++;
                    num5++;
                }

                var num7 = skylightValue;
                num4 = num;
                num5 = TerrainChunk.CalculateCellIndex(i, num, j);
                if (num7 > 0)
                {
                    while (num4 >= num2)
                    {
                        var cellValueFast3 = GetCellValue(chunk, num5, includePredictions);
                        var num8 = Terrain.ExtractContents(cellValueFast3);
                        if (num8 != 0)
                        {
                            var block = catalog.Blocks[num8];
                            if (!block.Transparent || block.LightAttenuation >= num7)
                            {
                                break;
                            }

                            num7 -= block.LightAttenuation;
                        }

                        cellValueFast3 = Terrain.ReplaceLight(cellValueFast3, num7);
                        changed |= SetCellLight(chunk, num5, Terrain.ExtractLight(cellValueFast3));
                        num4--;
                        num5--;
                    }
                }

                var num3 = num4 + 1;
                while (num4 >= num2)
                {
                    var cellValueFast4 = GetCellValue(chunk, num5, includePredictions);
                    cellValueFast4 = Terrain.ReplaceLight(cellValueFast4, 0);
                    changed |= SetCellLight(chunk, num5, Terrain.ExtractLight(cellValueFast4));
                    num4--;
                    num5--;
                }

                changed |= chunk.GetSunlightHeightFast(i, j) != num3;
                chunk.SetSunlightHeightFast(i, j, num3);
            }
        }

        return changed;
    }

    private void GenerateChunkLightSources(TerrainChunk chunk)
    {
        var blocks = _catalog.Blocks;
        for (var i = 0; i < 16; i++)
        {
            for (var j = 0; j < 16; j++)
            {
                var num = i + chunk.Origin.X;
                var num2 = j + chunk.Origin.Y;
                var chunkAtCell = _terrain.GetChunkAtCell(num - 1, num2, false);
                var chunkAtCell2 = _terrain.GetChunkAtCell(num + 1, num2, false);
                var chunkAtCell3 = _terrain.GetChunkAtCell(num, num2 - 1, false);
                var chunkAtCell4 = _terrain.GetChunkAtCell(num, num2 + 1, false);
                if (chunkAtCell == null || chunkAtCell2 == null || chunkAtCell3 == null || chunkAtCell4 == null)
                {
                    continue;
                }

                var topHeightFast = chunk.GetTopHeightFast(i, j);
                var bottomHeightFast = chunk.GetBottomHeightFast(i, j);
                var x = num - 1 - chunkAtCell.Origin.X;
                var z = num2 - chunkAtCell.Origin.Y;
                var x2 = num + 1 - chunkAtCell2.Origin.X;
                var z2 = num2 - chunkAtCell2.Origin.Y;
                var x3 = num - chunkAtCell3.Origin.X;
                var z3 = num2 - 1 - chunkAtCell3.Origin.Y;
                var x4 = num - chunkAtCell4.Origin.X;
                var z4 = num2 + 1 - chunkAtCell4.Origin.Y;
                var readable = _canReadNeighbor(chunkAtCell);
                var readable2 = _canReadNeighbor(chunkAtCell2);
                var readable3 = _canReadNeighbor(chunkAtCell3);
                var readable4 = _canReadNeighbor(chunkAtCell4);
                var shaftValueFast = readable ? chunkAtCell.GetShaftValueFast(x, z) : 0;
                var shaftValueFast2 = readable2 ? chunkAtCell2.GetShaftValueFast(x2, z2) : 0;
                var shaftValueFast3 = readable3 ? chunkAtCell3.GetShaftValueFast(x3, z3) : 0;
                var shaftValueFast4 = readable4 ? chunkAtCell4.GetShaftValueFast(x4, z4) : 0;
                var x5 = Terrain.ExtractSunlightHeight(shaftValueFast);
                var x6 = Terrain.ExtractSunlightHeight(shaftValueFast2);
                var x7 = Terrain.ExtractSunlightHeight(shaftValueFast3);
                var x8 = Terrain.ExtractSunlightHeight(shaftValueFast4);
                var num3 = MathUtils.Min(x5, x6, x7, x8);
                var num4 = bottomHeightFast;
                var num5 = TerrainChunk.CalculateCellIndex(i, bottomHeightFast, j);
                while (num4 <= topHeightFast)
                {
                    var cellValueFast = GetCellValue(chunk, num5, _includePredictions);
                    var num6 = 0;
                    var block = blocks[Terrain.ExtractContents(cellValueFast)];
                    if (num4 >= num3 && block.Transparent)
                    {
                        var cellLightFast = readable ? chunkAtCell.GetCellLightFast(x, num4, z) : 0;
                        var cellLightFast2 = readable2 ? chunkAtCell2.GetCellLightFast(x2, num4, z2) : 0;
                        var cellLightFast3 = readable3 ? chunkAtCell3.GetCellLightFast(x3, num4, z3) : 0;
                        var cellLightFast4 = readable4 ? chunkAtCell4.GetCellLightFast(x4, num4, z4) : 0;
                        num6 = MathUtils.Max(cellLightFast, cellLightFast2, cellLightFast3, cellLightFast4) -
                               1 -
                               block.LightAttenuation;
                    }

                    if (block.EmittedLightAmount > 0)
                    {
                        num6 = MathUtils.Max(num6, block.GetEmittedLightAmount(cellValueFast));
                    }

                    if (num6 > Terrain.ExtractLight(cellValueFast))
                    {
                        SetTrackedLight(chunk, num5, num6);
                        _lightSources.Enqueue(
                            new LightSource
                            {
                                X = num,
                                Y = num4,
                                Z = num2,
                                Light = num6
                            }
                        );
                    }

                    num4++;
                    num5++;
                }
            }
        }
    }

    private void GenerateChunkEdgeLightSources(TerrainChunk chunk, int face)
    {
        var x = 0;
        var z = 0;
        var neighborX = 0;
        var neighborZ = 0;
        TerrainChunk? neighbor;
        switch (face)
        {
            case 0:
                neighbor = _terrain.GetChunkAtCoords(chunk.Coords.X, chunk.Coords.Y + 1);
                z = TerrainChunk.SizeMinusOne;
                neighborZ = 0;
                break;
            case 1:
                neighbor = _terrain.GetChunkAtCoords(chunk.Coords.X + 1, chunk.Coords.Y);
                x = TerrainChunk.SizeMinusOne;
                neighborX = 0;
                break;
            case 2:
                neighbor = _terrain.GetChunkAtCoords(chunk.Coords.X, chunk.Coords.Y - 1);
                z = 0;
                neighborZ = TerrainChunk.SizeMinusOne;
                break;
            default:
                neighbor = _terrain.GetChunkAtCoords(chunk.Coords.X - 1, chunk.Coords.Y);
                x = 0;
                neighborX = TerrainChunk.SizeMinusOne;
                break;
        }

        if (neighbor == null ||
            !_canReadNeighbor(neighbor))
        {
            return;
        }

        for (var i = 0; i <= TerrainChunk.SizeMinusOne; i++)
        {
            switch (face)
            {
                case 0:
                case 2:
                    x = i;
                    neighborX = i;
                    break;
                default:
                    z = i;
                    neighborZ = i;
                    break;
            }

            var worldX = x + chunk.Origin.X;
            var worldZ = z + chunk.Origin.Y;
            var cellIndex = TerrainChunk.CalculateCellIndex(x, 0, z);
            var neighborCellIndex = TerrainChunk.CalculateCellIndex(neighborX, 0, neighborZ);
            for (var y = chunk.GetBottomHeightFast(x, z); y < (1 << TerrainChunk.HeightBits); y++)
            {
                var cellValue = GetCellValue(chunk, cellIndex + y, _includePredictions);
                var block = _catalog.Blocks[Terrain.ExtractContents(cellValue)];
                if (!block.Transparent)
                {
                    continue;
                }

                var light = Terrain.ExtractLight(GetCellValue(neighbor, neighborCellIndex + y, _includePredictions)) -
                            1;
                if (light <= Terrain.ExtractLight(cellValue))
                {
                    continue;
                }

                SetTrackedLight(chunk, cellIndex + y, light);
                if (light > 1)
                {
                    _lightSources.Enqueue(
                        new LightSource
                        {
                            X = worldX,
                            Y = y,
                            Z = worldZ,
                            Light = light
                        }
                    );
                }
            }
        }
    }

    private void PropagateLightSource(int x, int y, int z, int light)
    {
        var chunkAtCell = _terrain.GetChunkAtCell(x, z, false);
        if (chunkAtCell == null || !_canReadNeighbor(chunkAtCell))
        {
            return;
        }

        var index = TerrainChunk.CalculateCellIndex(x & 0xF, y, z & 0xF);
        var cellValueFast = GetCellValue(chunkAtCell, index, _includePredictions);
        var num = Terrain.ExtractContents(cellValueFast);
        var block = _catalog.Blocks[num];
        if (!block.Transparent)
        {
            return;
        }

        var num2 = light - block.LightAttenuation - 1;
        if (num2 <= Terrain.ExtractLight(cellValueFast))
        {
            return;
        }

        _lightSources.Enqueue(
            new LightSource
            {
                X = x,
                Y = y,
                Z = z,
                Light = num2
            }
        );
        SetTrackedLight(chunkAtCell, index, num2);
    }

    private struct LightSource
    {
        /// <summary>
        ///     X 坐标
        /// </summary>
        public int X;

        /// <summary>
        ///     Y 坐标
        /// </summary>
        public int Y;

        /// <summary>
        ///     Z 坐标
        /// </summary>
        public int Z;

        /// <summary>
        ///     光照强度值
        /// </summary>
        public int Light;
    }
}
