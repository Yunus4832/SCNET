using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Terrains.Distribution;
using Game.TerrainSerializers;

namespace Game.Subsystems;

public class SubsystemTerrain : Subsystem, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    private readonly Dictionary<Point3, bool> _modifiedCells = new();

    private readonly DynamicArray<Point3> _modifiedList = [];

    private static readonly Point3[] _neighborOffsets =
    [
        new(0, 0, 0),
        new(-1, 0, 0),
        new(1, 0, 0),
        new(0, -1, 0),
        new(0, 1, 0),
        new(0, 0, -1),
        new(0, 0, 1)
    ];

    private SubsystemPickables _subsystemPickables = null!;

    private SubsystemBlockBehaviors _subsystemBlockBehaviors = null!;

    private readonly List<BlockDropValue> _dropValues = [];

    public SubsystemGameInfo SubsystemGameInfo { get; set; } = null!;

    public SubsystemFurnitureBlockBehavior SubsystemFurnitureBlockBehavior { get; set; } = null!;

    public SubsystemPalette SubsystemPalette { get; set; } = null!;

    private WorldRuntimeContext Runtime => WorldRuntimeContext.For(Project);

    public BlockContentCatalog Catalog => WorldContentBinding.For(Runtime).Catalog;

    public Terrain Terrain { get; set; } = null!;

    public TerrainUpdater TerrainUpdater { get; set; } = null!;

    public ClientChunkContentCoordinator? ClientChunkContents { get; private set; }

    public IChunkContentTransport? ChunkContentTransport { get; private set; }

    public ClientTerrainDeltaCoordinator? ClientTerrainDeltas { get; private set; }

    public ClientTerrainPrediction? CellPrediction { get; private set; }

    public AuthoritativeChunkGenerationPipeline? AuthoritativeChunkGeneration { get; private set; }

    public IChunkContentAuthority? ChunkContentAuthority { get; private set; }

    public TerrainCellAuthority CellAuthority { get; private set; } = null!;

    public Terrain AuthoritativeTerrain => CellAuthority.Terrain;

    public TerrainContentRole ContentRole => ClientChunkContents != null
        ? TerrainContentRole.Replica
        : TerrainContentRole.Authority;

    public bool UsesClientChunkContent => ChunkContentTransport is ClientChunkContentTransport;

    public bool UsesChunkContentTransport => ChunkContentTransport != null;

    public TerrainSerializer24? TerrainSerializer { get; set; }

    public TerrainSaveCoordinator? TerrainSaveCoordinator { get; private set; }

    public ITerrainContentsGenerator TerrainContentsGenerator { get; set; } = null!;

    public UpdateOrder UpdateOrder => UpdateOrder.Terrain;

    public void ProcessModifiedCells()
    {
        _modifiedList.Count = 0;
        foreach (var key in _modifiedCells.Keys)
        {
            _modifiedList.Add(key);
        }

        _modifiedCells.Clear();
        for (var i = 0; i < _modifiedList.Count; i++)
        {
            var point = _modifiedList.Array[i];
            foreach (var point2 in _neighborOffsets)
            {
                var neighborChangedContext = new NeighborBlockChangedContext(
                    this,
                    point.X + point2.X,
                    point.Y + point2.Y,
                    point.Z + point2.Z,
                    point.X,
                    point.Y,
                    point.Z,
                    null
                );
                WorldRuleHooks.For(Project).Invoke(neighborChangedContext);
                if (!WorldRuntimeContext.IsProjectActive(neighborChangedContext.Project)) { return; }

                if (neighborChangedContext.Cancel)
                {
                    continue;
                }

                var cellContents = Terrain.GetCellContents(point.X + point2.X, point.Y + point2.Y, point.Z + point2.Z);
                var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(cellContents);
                foreach (var behavior in blockBehaviors)
                {
                    behavior.OnNeighborBlockChanged(
                        point.X + point2.X,
                        point.Y + point2.Y,
                        point.Z + point2.Z,
                        point.X,
                        point.Y,
                        point.Z
                    );
                }
            }
        }
    }

    public TerrainRaycastResult? Raycast(
        Vector3 start,
        Vector3 end,
        bool useInteractionBoxes,
        bool skipAirBlocks,
        Func<int, float, bool>? action,
        bool includePredictedBlocks = false
    )
    {
        var num = Vector3.Distance(start, end);
        if (num > 1000f)
        {
            Log.Warning("Terrain raycast too long, trimming.");
            end = start + 1000f * Vector3.Normalize(end - start);
        }

        var ray = new Ray3(start, Vector3.Normalize(end - start));
        var x = start.X;
        var y = start.Y;
        var z = start.Z;
        var x2 = end.X;
        var y2 = end.Y;
        var z2 = end.Z;
        var num2 = Terrain.ToCell(x);
        var num3 = Terrain.ToCell(y);
        var num4 = Terrain.ToCell(z);
        var num5 = Terrain.ToCell(x2);
        var num6 = Terrain.ToCell(y2);
        var num7 = Terrain.ToCell(z2);
        var num8 = x < x2 ? 1 : x > x2 ? -1 : 0;
        var num9 = y < y2 ? 1 : y > y2 ? -1 : 0;
        var num10 = z < z2 ? 1 : z > z2 ? -1 : 0;
        var num11 = MathUtils.Floor(x);
        var num12 = num11 + 1f;
        var num13 = (x > x2 ? x - num11 : num12 - x) / Math.Abs(x2 - x);
        var num14 = MathUtils.Floor(y);
        var num15 = num14 + 1f;
        var num16 = (y > y2 ? y - num14 : num15 - y) / Math.Abs(y2 - y);
        var num17 = MathUtils.Floor(z);
        var num18 = num17 + 1f;
        var num19 = (z > z2 ? z - num17 : num18 - z) / Math.Abs(z2 - z);
        var num20 = 1f / Math.Abs(x2 - x);
        var num21 = 1f / Math.Abs(y2 - y);
        var num22 = 1f / Math.Abs(z2 - z);
        while (true)
        {
            BoundingBox boundingBox = default;
            var collisionBoxIndex = 0;
            float? num23 = null;
            var cellValue = Terrain.GetCellValue(num2, num3, num4);
            if (includePredictedBlocks)
            {
                var predictedValue = Terrain.GetCellValueForGeometry(num2, num3, num4);
                if (Terrain.ExtractContents(predictedValue) != 0)
                {
                    cellValue = predictedValue;
                }
            }

            var num24 = Terrain.ExtractContents(cellValue);
            if (num24 != 0 || !skipAirBlocks)
            {
                var ray2 = new Ray3(ray.Position - new Vector3(num2, num3, num4), ray.Direction);
                var num25 = Catalog.Blocks[num24].Raycast(
                    ray2,
                    this,
                    cellValue,
                    useInteractionBoxes,
                    out var nearestBoxIndex,
                    out var nearestBox
                );
                if (num25.HasValue && (!num23.HasValue || num25.Value < num23.Value))
                {
                    num23 = num25;
                    collisionBoxIndex = nearestBoxIndex;
                    boundingBox = nearestBox;
                }
            }

            if (num23.HasValue && num23.Value <= num && (action == null || action(cellValue, num23.Value)))
            {
                var face = 0;
                var vector = start - new Vector3(num2, num3, num4) + num23.Value * ray.Direction;
                var num26 = float.MaxValue;
                var num27 = MathUtils.Abs(vector.X - boundingBox.Min.X);
                if (num27 < num26)
                {
                    num26 = num27;
                    face = 3;
                }

                num27 = MathUtils.Abs(vector.X - boundingBox.Max.X);
                if (num27 < num26)
                {
                    num26 = num27;
                    face = 1;
                }

                num27 = MathUtils.Abs(vector.Y - boundingBox.Min.Y);
                if (num27 < num26)
                {
                    num26 = num27;
                    face = 5;
                }

                num27 = MathUtils.Abs(vector.Y - boundingBox.Max.Y);
                if (num27 < num26)
                {
                    num26 = num27;
                    face = 4;
                }

                num27 = MathUtils.Abs(vector.Z - boundingBox.Min.Z);
                if (num27 < num26)
                {
                    num26 = num27;
                    face = 2;
                }

                num27 = MathUtils.Abs(vector.Z - boundingBox.Max.Z);
                if (num27 < num26)
                {
                    face = 0;
                }

                TerrainRaycastResult value = default;
                value.Ray = ray;
                value.Value = cellValue;
                value.CellFace = new CellFace
                {
                    X = num2,
                    Y = num3,
                    Z = num4,
                    Face = face
                };
                value.CollisionBoxIndex = collisionBoxIndex;
                value.Distance = num23.Value;
                return value;
            }

            if (num13 <= num16 && num13 <= num19)
            {
                if (num2 == num5)
                {
                    break;
                }

                num13 += num20;
                num2 += num8;
            }
            else if (num16 <= num13 && num16 <= num19)
            {
                if (num3 == num6)
                {
                    break;
                }

                num16 += num21;
                num3 += num9;
            }
            else
            {
                if (num4 == num7)
                {
                    break;
                }

                num19 += num22;
                num4 += num10;
            }
        }

        return null;
    }

    public List<TerrainChunk> GetVisibleChunks(Vector3 viewPosition, Vector3 viewDirection)
    {
        var vector = Vector3.Normalize(Vector3.Cross(viewDirection, Vector3.UnitY));
        var v = Vector3.Normalize(Vector3.Cross(viewDirection, vector));
        var obj = new[]
        {
            viewPosition,
            viewPosition + 6f * viewDirection,
            viewPosition + 6f * viewDirection - 6f * vector,
            viewPosition + 6f * viewDirection + 6f * vector,
            viewPosition + 6f * viewDirection - 2f * v,
            viewPosition + 6f * viewDirection + 2f * v
        };
        var list = new List<TerrainChunk>();
        foreach (var vector2 in obj)
        {
            var chunkAtCell = Terrain.GetChunkAtCell(
                Terrain.ToCell(vector2.X),
                Terrain.ToCell(vector2.Z),
                false
            );
            if (chunkAtCell is { MainThreadState: TerrainChunkState.Ready } && !list.Contains(chunkAtCell))
            {
                list.Add(chunkAtCell);
            }
        }

        return list;
    }

    public void ChangeCellNet(
        int x,
        int y,
        int z,
        int value,
        bool updateModificationCounter = true,
        ComponentMiner? miner = null
    )
    {
        CellPrediction?.OnCellChanged(new Point3(x, y, z));
        var authorityChunk = Runtime.IsAuthority
            ? CellAuthority.Terrain.GetChunkAtCell(x, z, false)
            : null;
        var baseContentVersion = authorityChunk?.NetworkContentRevision + 1 ?? 0;
        if (!ReferenceEquals(CellAuthority.Terrain, Terrain))
        {
            CellAuthority.ChangeCell(x, y, z, value, updateModificationCounter);
        }

        var cellValueFast = Terrain.GetCellValueFast(x, y, z);
        value = Terrain.ReplaceLight(value, 0);
        cellValueFast = Terrain.ReplaceLight(cellValueFast, 0);
        if (value == cellValueFast)
        {
            PublishAuthoritativeCellDelta(
                authorityChunk,
                baseContentVersion,
                x,
                y,
                z,
                value
            );
            return;
        }

        Terrain.SetCellValueFast(x, y, z, value);
        PublishAuthoritativeCellDelta(
            authorityChunk,
            baseContentVersion,
            x,
            y,
            z,
            value
        );
        var chunkAtCell = Terrain.GetChunkAtCell(x, z, false);
        if (chunkAtCell != null)
        {
            if (updateModificationCounter)
            {
                chunkAtCell.ModificationCounter++;
            }

            TerrainUpdater.DowngradeChunkNeighborhoodState(
                chunkAtCell.Coords,
                1,
                TerrainChunkState.InvalidLight,
                false
            );
        }

        _modifiedCells[new Point3(x, y, z)] = true;
        var num = Terrain.ExtractContents(cellValueFast);
        var num2 = Terrain.ExtractContents(value);
        if (num2 != num)
        {
            var removedContext = new BlockRemovedContext(this, x, y, z, cellValueFast, value, miner);
            WorldRuleHooks.For(Project).Invoke(removedContext);
            if (!WorldRuntimeContext.IsProjectActive(removedContext.Project)) { return; }

            if (!removedContext.Cancel)
            {
                var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(num);
                foreach (var behavior in blockBehaviors)
                {
                    behavior.OnBlockRemoved(cellValueFast, value, x, y, z);
                }
            }

            var addedContext = new BlockAddedContext(this, x, y, z, value, cellValueFast, miner);
            WorldRuleHooks.For(Project).Invoke(addedContext);
            if (!WorldRuntimeContext.IsProjectActive(addedContext.Project)) { return; }

            if (addedContext.Cancel)
            {
                return;
            }

            {
                var blockBehaviors2 = _subsystemBlockBehaviors.GetBlockBehaviors(num2);
                foreach (var behavior in blockBehaviors2)
                {
                    behavior.OnBlockAdded(value, cellValueFast, x, y, z);
                    if (miner != null)
                    {
                        behavior.OnBlockAdded(value, cellValueFast, x, y, z, miner);
                    }
                }
            }
        }
        else
        {
            var modifiedContext = new BlockModifiedContext(this, x, y, z, value, cellValueFast, miner);
            WorldRuleHooks.For(Project).Invoke(modifiedContext);
            if (!WorldRuntimeContext.IsProjectActive(modifiedContext.Project)) { return; }

            if (modifiedContext.Cancel)
            {
                return;
            }

            var blockBehaviors3 = _subsystemBlockBehaviors.GetBlockBehaviors(num2);
            foreach (var behavior in blockBehaviors3)
            {
                behavior.OnBlockModified(value, cellValueFast, x, y, z);
            }
        }
    }

    private void PublishAuthoritativeCellDelta(
        TerrainChunk? authorityChunk,
        long baseContentVersion,
        int x,
        int y,
        int z,
        int value
    )
    {
        if (authorityChunk == null)
        {
            return;
        }

        var resultContentVersion = authorityChunk.NetworkContentRevision + 1;
        if (resultContentVersion <= baseContentVersion)
        {
            return;
        }

        var delta = new TerrainCellDelta(
            new Point3(x, y, z),
            value,
            baseContentVersion,
            resultContentVersion
        );
        ClientTerrainDeltas?.Receive(delta);
        if (Runtime.IsAuthority)
        {
            NetworkSender.SendToChunkObservers(
                Project,
                authorityChunk.Coords,
                new TerrainCellDeltaPackage { CellDeltas = [delta] }
            );
        }
    }

    public void ChangeCell(
        int x,
        int y,
        int z,
        int value,
        bool updateModificationCounter = true,
        ComponentMiner? miner = null
    )
    {
        if (!Runtime.IsAuthority)
        {
            return;
        }

        if (TryPrepareCellChange(x, y, z, ref value, miner))
        {
            ChangeCellNet(x, y, z, value, updateModificationCounter, miner);
        }
    }

    private bool TryPrepareCellChange(int x, int y, int z, ref int value, ComponentMiner? miner)
    {
        var changingContext = new TerrainCellChangingContext(
            this,
            x,
            y,
            z,
            CellAuthority.GetCellValue(x, y, z),
            value,
            miner
        );
        WorldRuleHooks.For(Project).Invoke(changingContext);
        if (!WorldRuntimeContext.IsProjectActive(changingContext.Project)) { return false; }

        if (changingContext.Cancel)
        {
            return false;
        }

        value = changingContext.NewValue;
        return true;
    }

    public void DestroyCell(
        int toolLevel,
        int x,
        int y,
        int z,
        int newValue,
        bool noDrop,
        bool noParticleSystem,
        ComponentMiner? miner = null
    )
    {
        if (!Runtime.IsAuthority)
        {
            return;
        }

        // Authorize before drops, particles and harvest callbacks, not after their side effects.
        if (!TryPrepareCellChange(x, y, z, ref newValue, miner))
        {
            return;
        }

        var cellValue = Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractContents(cellValue);
        var block = Catalog.Blocks[num];
        if (num != 0)
        {
            var showDebris = true;
            if (!noDrop)
            {
                _dropValues.Clear();
                block.GetDropValues(this, cellValue, newValue, toolLevel, _dropValues, out showDebris);
                foreach (var item in _dropValues)
                {
                    var dropValue = item;
                    if (dropValue.Count <= 0)
                    {
                        continue;
                    }

                    var harvestedContext = new ItemHarvestedContext(this, x, y, z, cellValue, dropValue, newValue);
                    WorldRuleHooks.For(Project).Invoke(harvestedContext);
                    if (!WorldRuntimeContext.IsProjectActive(harvestedContext.Project)) { return; }

                    if (harvestedContext.Cancel)
                    {
                        continue;
                    }

                    dropValue = harvestedContext.DropValue;
                    newValue = harvestedContext.NewBlockValue;

                    var blockBehaviors =
                        _subsystemBlockBehaviors.GetBlockBehaviors(Terrain.ExtractContents(dropValue.Value));
                    foreach (var behavior in blockBehaviors)
                    {
                        behavior.OnItemHarvested(x, y, z, cellValue, ref dropValue, ref newValue);
                    }

                    if (dropValue.Count <= 0 || Terrain.ExtractContents(dropValue.Value) == 0)
                    {
                        continue;
                    }

                    var position = new Vector3(x, y, z) + new Vector3(0.5f);
                    _subsystemPickables.AddPickable(dropValue.Value, dropValue.Count, position, null, null);
                }
            }

            if (showDebris && !noParticleSystem)
            {
                RequestDestructionDebris(cellValue, new Vector3(x + 0.5f, y + 0.5f, z + 0.5f), 1f);
            }
        }

        ChangeCellNet(x, y, z, newValue, true, miner);
    }

    public event Action<int, Vector3, float>? DestructionDebrisRequested;

    public void RequestDestructionDebris(int value, Vector3 position, float strength)
    {
        if (WorldRuntimeContext.IsProjectActive(Project))
        {
            DestructionDebrisRequested?.Invoke(value, position, strength);
        }
    }

    public void Update(float dt)
    {
        CellPrediction?.Update(Time.RealTime);
        TerrainUpdater.Update();
        ProcessModifiedCells();
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        SubsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        _subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true)!;
        SubsystemFurnitureBlockBehavior = Project.FindSubsystem<SubsystemFurnitureBlockBehavior>(true)!;
        SubsystemPalette = Project.FindSubsystem<SubsystemPalette>(true)!;
        Terrain = new Terrain();
        CellAuthority = new TerrainCellAuthority(Terrain);
        if (!Runtime.IsAuthority)
        {
            ClientChunkContents = new ClientChunkContentCoordinator(Terrain);
            ChunkContentTransport = new ClientChunkContentTransport(Runtime);
        }

        if (Runtime.IsAuthority)
        {
            var serializer = new TerrainSerializer24(SubsystemGameInfo.DirectoryName);
            TerrainSerializer = serializer;
            TerrainSaveCoordinator = new TerrainSaveCoordinator(serializer.SaveSnapshot);
        }

        TerrainContentsGenerator = CreateTerrainContentsGenerator(Terrain);

        if (Runtime.IsAuthority)
        {
            AuthoritativeChunkGeneration = new AuthoritativeChunkGenerationPipeline(
                TerrainContentsGenerator,
                chunk => TerrainSaveCoordinator?.TryRestorePendingSnapshot(chunk) == true,
                (TerrainSerializer ??
                 throw new InvalidOperationException("Authority terrain requires a disk serializer.")).LoadChunk,
                chunk =>
                {
                    WorldRuleHooks.For(Project).Invoke(new TerrainChunkGeneratedContext(this, chunk));
                    return WorldRuntimeContext.IsProjectActive(Project);
                }
            );
            ChunkContentAuthority = new TerrainChunkContentAuthority(Terrain);
        }

        // Start the worker only after every service it can reach has been composed.
        TerrainUpdater = new TerrainUpdater(this);
        if (!Runtime.IsAuthority)
        {
            CellPrediction = new ClientTerrainPrediction(
                Terrain,
                coords =>
                    TerrainUpdater.DowngradeChunkNeighborhoodState(coords, 1, TerrainChunkState.InvalidLight, false)
            );
            ClientChunkContents!.ContentInstalled += CellPrediction.OnContentInstalled;
        }

        if (ClientChunkContents != null)
        {
            ClientTerrainDeltas = new ClientTerrainDeltaCoordinator(
                Terrain,
                delta => ChangeCellNet(
                    delta.Cell.X,
                    delta.Cell.Y,
                    delta.Cell.Z,
                    delta.Value
                )
            );
            ClientChunkContents.ContentInstalled += TerrainUpdater.OnNetworkChunkContentInstalled;
            // Replay deltas after the ordinary snapshot derivation reset. A detected version gap
            // must retain its required baseline version after the installation.
            ClientChunkContents.ContentInstalled += chunk => ClientTerrainDeltas.OnContentInstalled(chunk);
        }
    }

    internal ITerrainContentsGenerator CreateTerrainContentsGenerator(Terrain terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        var terrainGenerationMode = SubsystemGameInfo.WorldSettings.TerrainGenerationMode;
        if (TerrainGenerationModes.IsFlat(terrainGenerationMode))
        {
            return new TerrainContentsGeneratorFlat(this, terrain);
        }

        if (terrainGenerationMode is TerrainGenerationMode.LegacyContinent22 or TerrainGenerationMode.LegacyIsland22)
        {
            return new TerrainContentsGenerator22(this, terrain);
        }

        if (terrainGenerationMode is TerrainGenerationMode.LegacyContinent23 or TerrainGenerationMode.LegacyIsland23)
        {
            return new TerrainContentsGenerator23(this, terrain);
        }

        if (terrainGenerationMode is TerrainGenerationMode.LegacyContinentPre21
            or TerrainGenerationMode.LegacyIslandPre21
            or TerrainGenerationMode.LegacyContinent21 or TerrainGenerationMode.LegacyIsland21)
        {
            return new TerrainContentsGenerator21(this, terrain);
        }

        return new TerrainContentsGenerator24(this, terrain);
    }

    private void SaveChunk()
    {
        if (!Runtime.IsAuthority) { return; }

        var serializer = TerrainSerializer ??
                         throw new InvalidOperationException("Authority terrain requires a disk serializer.");
        TerrainUpdater.UpdateEvent.WaitOne();
        try
        {
            TerrainSaveCoordinator?.Flush();
            var allocatedChunks = AuthoritativeTerrain.AllocatedChunks;
            foreach (var chunk in allocatedChunks)
            {
                serializer.SaveChunk(chunk);
            }
        }
        finally
        {
            TerrainUpdater.UpdateEvent.Set();
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        SaveChunk();
    }

    public event Action<SubsystemTerrain>? Closing;

    private bool _closed;

    public override void Dispose()
    {
        if (_closed) { return; }

        _closed = true;
        var errors = new List<Exception>();
        foreach (var handler in Closing?.GetInvocationList() ?? [])
        {
            TryCleanup(() => ((Action<SubsystemTerrain>)handler)(this));
        }

        Closing = null;
        TryCleanup(() => TerrainUpdater?.Dispose());
        TerrainUpdater = null!;
        TryCleanup(() => TerrainSaveCoordinator?.Dispose());
        TerrainSaveCoordinator = null;
        TryCleanup(() => TerrainSerializer?.Dispose());
        TerrainSerializer = null;
        TryCleanup(() => Terrain?.Dispose());
        Terrain = null!;
        if (errors.Count > 0) { throw new AggregateException("Could not dispose all terrain resources.", errors); }

        void TryCleanup(Action cleanup)
        {
            try { cleanup(); }
            catch (Exception exception) { errors.Add(exception); }
        }
    }
}
