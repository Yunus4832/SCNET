using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network.Chunks;
using Game.Terrains.Distribution;
using Game.Terrains.Execution;
using Game.Terrains.Lighting;
using Game.TerrainSerializers;

namespace Game.Subsystems;

public class SubsystemTerrain : Subsystem, IUpdateable
{
    public SubsystemTerrain()
    {
        WorkExecutor = new TerrainWorkExecutor(() => Computations.Step(), Access);
    }

    public TerrainAccessGate Access { get; } = new();

    public TerrainWorkExecutor WorkExecutor { get; }

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

    public TerrainComputationScheduler Computations { get; private set; } = null!;

    public TerrainEnvironmentObserver EnvironmentObserver { get; private set; } = null!;

    private TerrainChunkBehaviorNotifier _notifications = null!;

    private TerrainMainWorkRequest? _locationRequest;

    private long _terrainInteractionRevision;

    public long TerrainInteractionRevision => Volatile.Read(ref _terrainInteractionRevision);

    public event Action<TerrainChunk, bool>? MeshInvalidated;

    public event Action? TerrainAppearanceChanged;

    public void NotifyTerrainInteraction() => Interlocked.Increment(ref _terrainInteractionRevision);

    public void InvalidateMesh(Point2 coordinates, bool forceCache)
    {
        var chunk = Terrain.GetChunkAtCoords(coordinates.X, coordinates.Y);
        if (chunk != null) { MeshInvalidated?.Invoke(chunk, forceCache); }
    }

    public TerrainDemandManager Demands { get; private set; } = null!;

    public TerrainResidencyManager Residency { get; private set; } = null!;

    public ServerChunkDistributionScheduler? ChunkDistribution { get; private set; }

    public ClientChunkContentCoordinator? ClientChunkContents { get; private set; }

    public IChunkContentTransport? ChunkContentTransport { get; private set; }

    public ClientTerrainDeltaCoordinator? ClientTerrainDeltas { get; private set; }

    public ClientTerrainContentPump? ClientContentPump { get; private set; }

    public ClientTerrainPrediction? CellPrediction { get; private set; }

    public AuthorityTerrainContent? AuthorityContents { get; private set; }

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

    private ITerrainContentsGenerator? _contentsGenerator;

    public ITerrainEnvironment Environment { get; private set; } = null!;

    public ITerrainContentsGenerator TerrainContentsGenerator => _contentsGenerator ??
                                                                 throw new InvalidOperationException(
                                                                     "Replica terrain does not own an authoritative contents generator."
                                                                 );

    public UpdateOrder UpdateOrder => UpdateOrder.Terrain;

    public void SetUpdateLocation(int index, Vector2 center, float visibility, float content)
    {
        if (UsesClientChunkContent)
        {
            var previous = Demands.GetRequestedLocation(index);
            if (previous.LastChunksUpdateCenter is { } last &&
                visibility.CloseTo(previous.VisibilityDistance) &&
                MathUtils.Max(content, visibility).CloseTo(previous.ContentDistance) &&
                Vector2.DistanceSquared(center, last) <=
                MathUtils.Sqr(NetworkTerrainPolicy.ClientTerrainPreparationMargin)) { return; }
        }

        if (!Demands.SetLocation(index, center, visibility, content)) { return; }

        if (UsesClientChunkContent)
        {
            NetworkSender.SendToServer(
                WorldRuntimeContext.For(Project),
                new CharacterUpdateLocationPackage { UpdateLocation = Demands.GetRequestedLocation(index) }
            );
        }
    }

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
            if (chunkAtCell is { HasCurrentLight: true } && !list.Contains(chunkAtCell))
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
        int cellValueFast;
        WorkExecutor.EnterMain();
        try
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

            cellValueFast = Terrain.GetCellValueFast(x, y, z);
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

                Computations.InvalidateContentNeighborhood(chunkAtCell.Coords, 1);
            }

            _modifiedCells[new Point3(x, y, z)] = true;
        }
        finally { WorkExecutor.LeaveMain(); }

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
        if (authorityChunk is not { HasContents: true, IsContentInitialized: true })
        {
            // Initial generation and block normalization customize the first baseline, not live deltas.
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
        if (!IsTerrainActive) { return; }

        ClientContentPump?.TryDrainReceivedContents();
        if (!IsTerrainActive) { return; }

        if (AuthorityContents?.PublishGeneratedContents() > 0) { WorkExecutor.Wake(); }

        if (!IsTerrainActive) { return; }

        ChunkDistribution?.Update();
        if (!IsTerrainActive) { return; }

        var environment = EnvironmentObserver.Update();
        if (environment.SunlightChanged) { Computations.InvalidateSkylight(); }

        if (!IsTerrainActive) { return; }

        if (environment.AppearanceChanged) { TerrainAppearanceChanged?.Invoke(); }

        if (!IsTerrainActive) { return; }

        ApplyTerrainDemand();
        if (!IsTerrainActive) { return; }

        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < TimeSpan.FromMilliseconds(4) &&
               Computations.TryTakeInitialization(out var initialized))
        {
            _notifications.TryInitialize(initialized!);
            if (!IsTerrainActive) { return; }
        }

        ClientContentPump?.UpdateRequests();
        if (!IsTerrainActive) { return; }

        ProcessModifiedCells();
    }

    private bool IsTerrainActive => !_closed && WorldRuntimeContext.IsProjectActive(Project);

    private void ApplyTerrainDemand()
    {
        if (Residency.IsUpdating || !Demands.HasChanges) { return; }

        if (!_locationRequest!.TryEnter(1)) { return; }

        try
        {
            var demand = Demands.CapturePending();
            Demands.Apply(demand);
            var allocation = Residency.Update(demand.EffectiveLocations, _locationRequest);
            if (!IsTerrainActive) { return; }

            Computations.UpdateDemand(demand.EffectiveLocations);
            if (!allocation.RetryRequired) { Demands.Reconciled(demand); }
        }
        finally
        {
            if (_locationRequest.HasAccess) { _locationRequest.Leave(); }
        }
    }

    private void OnChunkAllocated(Point2 coordinates)
    {
        var chunk = Terrain.GetChunkAtCoords(coordinates.X, coordinates.Y)!;
        Computations.Request(chunk);
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

        Environment = TerrainEnvironment.Create(SubsystemGameInfo.WorldSettings, SubsystemGameInfo.WorldSeed);

        if (Runtime.IsAuthority)
        {
            _contentsGenerator = CreateTerrainContentsGenerator(Terrain);
            var generation = new AuthoritativeChunkGenerationPipeline(
                TerrainContentsGenerator,
                chunk => TerrainSaveCoordinator?.TryRestorePendingSnapshot(chunk) == true,
                (TerrainSerializer ??
                 throw new InvalidOperationException("Authority terrain requires a disk serializer.")).LoadChunk
            );
            AuthorityContents = new AuthorityTerrainContent(this, generation);
            ChunkContentAuthority = new TerrainChunkContentAuthority(Terrain);
        }

        Demands = new TerrainDemandManager(Terrain, ContentRole);
        if (Runtime.IsAuthority)
        {
            ChunkDistribution = new ServerChunkDistributionScheduler(Project, ChunkContentAuthority!);
        }

        // Compose every service and callback before the worker is started below.
        _notifications = new TerrainChunkBehaviorNotifier(this, _subsystemBlockBehaviors);
        _locationRequest = WorkExecutor.CreateMainRequest();
        EnvironmentObserver = new TerrainEnvironmentObserver(
            Terrain,
            SubsystemGameInfo,
            Project.FindSubsystem<SubsystemSky>(true)!
        );
        Computations = new TerrainComputationScheduler(
            this,
            new TerrainLightingCalculator(
                Terrain,
                Catalog,
                ContentRole == TerrainContentRole.Replica,
                static chunk => chunk.HasContents && !chunk.NeedsContentValidation
            )
        );
        if (AuthorityContents != null) { AuthorityContents.ContentPublished += Computations.ContentAvailable; }

        if (!Runtime.IsAuthority)
        {
            CellPrediction = new ClientTerrainPrediction(
                Terrain,
                coords =>
                    Computations.InvalidateContentNeighborhood(coords, 1)
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
            ClientContentPump = new ClientTerrainContentPump(
                this,
                ChunkContentTransport ?? throw new InvalidOperationException("Client content requires a transport."),
                ClientChunkContents,
                ClientTerrainDeltas
            );
            ClientChunkContents.ContentInstalled += Computations.ContentAvailable;
            ClientChunkContents.ContentInstalled += ClientContentPump.OnContentInstalled;
            // Replay after derivation invalidation and delivery metadata reset.
            ClientChunkContents.ContentInstalled += chunk => ClientTerrainDeltas.OnContentInstalled(chunk);
        }

        Residency = new TerrainResidencyManager(this, _notifications);
        Residency.ChunkAllocated += OnChunkAllocated;
        Residency.ChunkReleasing += Computations.Retired;
        Residency.ChunkAllocationRenewed += Computations.Renewed;
        WorkExecutor.Wake();
        WorkExecutor.Start(Runtime.Lifetime);
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

    public Task PendingTerrainSave { get; private set; } = Task.CompletedTask;

    private void CaptureTerrainSave()
    {
        if (!Runtime.IsAuthority || Project.SendToClientMode) { return; }

        var saves = TerrainSaveCoordinator ??
                    throw new InvalidOperationException("Authority terrain requires a save coordinator.");
        WorkExecutor.EnterMain();
        try
        {
            PendingTerrainSave = saves.CaptureAndQueueSave(AuthoritativeTerrain.AllocatedChunks);
        }
        finally
        {
            WorkExecutor.LeaveMain();
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        CaptureTerrainSave();
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
        TryCleanup(() => WorkExecutor.Dispose());
        TryCleanup(() => ClientContentPump?.Dispose());
        TryCleanup(() => _locationRequest?.Dispose());
        TryCleanup(() => Computations?.Dispose());
        TryCleanup(() => AuthorityContents?.Dispose());
        TryCleanup(() => ChunkDistribution?.Dispose());
        TryCleanup(() => Demands?.Dispose());
        TryCleanup(() => TerrainSaveCoordinator?.Dispose());
        TerrainSaveCoordinator = null;
        TryCleanup(() => TerrainSerializer?.Dispose());
        TerrainSerializer = null;
        TryCleanup(() => Terrain?.Dispose());
        TryCleanup(() => Access.Dispose());
        Terrain = null!;
        if (errors.Count > 0) { throw new AggregateException("Could not dispose all terrain resources.", errors); }

        void TryCleanup(Action cleanup)
        {
            try { cleanup(); }
            catch (Exception exception) { errors.Add(exception); }
        }
    }
}
