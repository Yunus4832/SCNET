using Game.Network.Chunks;
using Game.Terrains.Distribution;
using Game.TerrainSerializers;

namespace Game.Terrains;

/// <summary>
///     地形更新类
/// </summary>
public class TerrainUpdater
{
    public const double NetworkChunkRequestRetrySeconds = 5.0;

    /// <summary>
    ///     光源列表
    /// </summary>
    private readonly DynamicArray<LightSource> _lightSources = [];

    private ITerrainDerivedWork? _derivedWork;

    /// <summary>
    ///     暂停/恢复更新线程的信号量
    /// </summary>
    private readonly ManualResetEvent _pauseEvent = new(true);

    /// <summary>
    ///     更新事件信号，用于同步主线程和更新线程
    /// </summary>
    public AutoResetEvent UpdateEvent { get; } = new(true);

    /// <summary>
    ///     待处理的 Location 位置字典
    /// </summary>
    private readonly Dictionary<int, TerrainUpdateLocation?> _pendingLocations = new();

    private readonly Dictionary<Guid, TerrainSpawnPreparation> _spawnPreparations = new();

    private bool _preparationsChanged;

    private bool _disposed;

    public TerrainSpawnPreparation BeginSpawnPreparation(Guid characterId, Vector2 center, float radius)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_subsystemTerrain.ContentRole != TerrainContentRole.Authority)
        {
            throw new InvalidOperationException("Spawn preparation requires authority terrain.");
        }

        if (characterId == Guid.Empty)
        {
            throw new ArgumentException("A character owner is required.", nameof(characterId));
        }

        ValidatePreparation(center, radius);
        if (_spawnPreparations.ContainsKey(characterId))
        {
            throw new InvalidOperationException("Character already owns a spawn preparation.");
        }

        var preparation = new TerrainSpawnPreparation(this, characterId, center, radius);
        _spawnPreparations.Add(characterId, preparation);
        _preparationsChanged = true;
        return preparation;
    }

    internal void UpdateSpawnPreparation(TerrainSpawnPreparation preparation, Vector2 center, float radius)
    {
        RequirePreparation(preparation);
        ValidatePreparation(center, radius);
        if (preparation.Location.Center == center && preparation.Location.ContentDistance == radius) { return; }

        preparation.SetLocation(center, radius);
        _preparationsChanged = true;
    }

    internal void ReleaseSpawnPreparation(TerrainSpawnPreparation preparation)
    {
        if (_spawnPreparations.GetValueOrDefault(preparation.CharacterId) != preparation) { return; }

        _spawnPreparations.Remove(preparation.CharacterId);
        _preparationsChanged = true;
    }

    internal float GetSpawnPreparationProgress(TerrainSpawnPreparation preparation, float visibility, float content)
    {
        RequirePreparation(preparation);
        return GetUpdateProgress(preparation.Location, visibility, content);
    }

    private void RequirePreparation(TerrainSpawnPreparation preparation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_spawnPreparations.GetValueOrDefault(preparation.CharacterId) != preparation)
        {
            throw new InvalidOperationException("Spawn preparation no longer belongs to this terrain lifetime.");
        }
    }

    private static void ValidatePreparation(Vector2 center, float radius)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(center));
        }

        if (!float.IsFinite(radius) || radius <= 0) { throw new ArgumentOutOfRangeException(nameof(radius)); }
    }

    internal TerrainUpdateLocation[] CaptureEffectiveLocations() => _updateParameters.Locations.Values
        .Concat(_spawnPreparations.Values.Select(preparation => preparation.Location)).ToArray();

    public bool IsUpdateLocationCovered(int locationIndex, Vector2 center, float radius)
    {
        if (_pendingLocations.TryGetValue(locationIndex, out var pending))
        {
            return pending is { } location && Covers(location, center, radius);
        }

        return _updateParameters.Locations.TryGetValue(locationIndex, out var current) &&
               Covers(current, center, radius);

        static bool Covers(TerrainUpdateLocation location, Vector2 point, float distance) =>
            Vector2.Distance(location.Center, point) + distance <= location.ContentDistance;
    }

    private readonly HashSet<Point2> _inactiveClientChunks = [];

    private readonly SubsystemBlockBehaviors _subsystemBlockBehaviors;

    private readonly SubsystemTerrain _subsystemTerrain;

    private readonly Terrain _terrain;

    /// <summary>
    ///     恢复更新线程的锁对象
    /// </summary>
    private readonly Lock _unpauseLock = new();

    /// <summary>
    ///     更新参数的锁对象
    /// </summary>
    private readonly Lock _updateParametersLock = new();

    private ClientChunkContentApplication? _clientContentApplication;

    private readonly Dictionary<Point2, long> _remoteChunkLastAccess = [];

    private long _remoteChunkAccessClock;

    /// <summary>
    ///     上一次的天空光照值，用于检测光照变化
    /// </summary>
    private int _lastSkylightValue;

    /// <summary>
    ///     是否退出更新线程
    /// </summary>
    private volatile bool _quitUpdateThread;

    /// <summary>
    ///     游戏信息子系统
    /// </summary>
    private readonly SubsystemGameInfo _subsystemGameInfo;


    /// <summary>
    ///     天空子系统
    /// </summary>
    private readonly SubsystemSky _subsystemSky;

    /// <summary>
    ///     请求同步更新的帧索引
    /// </summary>
    private long _synchronousUpdateRevision;

    public long SynchronousUpdateRevision => Volatile.Read(ref _synchronousUpdateRevision);

    /// <summary>
    ///     地形更新后台任务
    /// </summary>
    private readonly Task _task;

    /// <summary>
    ///     更新线程中使用的更新参数的引用
    /// </summary>
    private UpdateParameters _threadUpdateParameters;

    /// <summary>
    ///     是否请求恢复更新线程
    /// </summary>
    private bool _unpauseUpdateThread;

    /// <summary>
    ///     更新参数
    /// </summary>
    private UpdateParameters _updateParameters;

    /// <summary>
    ///     温度曲线
    /// </summary>
    private FloatCurve _temperatureCurve = new(
        new Vector2(0f, 0f),
        new Vector2(0.125f, 0f),
        new Vector2(0.25f, 0f),
        new Vector2(0.375f, -4f),
        new Vector2(0.5f, -12f),
        new Vector2(0.625f, -24f),
        new Vector2(0.75f, -12f),
        new Vector2(0.875f, -4f),
        new Vector2(1f, 0f)
    );

    /// <summary>
    ///     湿度曲线
    /// </summary>
    private FloatCurve _humidityCurve = new(
        new Vector2(0f, 0f),
        new Vector2(0.25f, 0f),
        new Vector2(0.5f, 0f),
        new Vector2(0.75f, 0f),
        new Vector2(1f, 0f)
    );

    public ServerChunkDistributionScheduler? ServerChunkDistribution { get; }

    public int PendingLocationCount => _pendingLocations.Count;

    public long LocationHandoffTimeouts { get; private set; }

    public long DeferredAllocationPasses { get; private set; }

    private int _chunkSchedulingStep;

    private double _lastNetworkChunkDiagnosticTime;

    /// <summary>
    ///     更新位置字典
    /// </summary>
    public Dictionary<int, TerrainUpdateLocation> UpdateLocations => _updateParameters.Locations;

    /// <summary>
    ///     区块初始化完成事件
    /// </summary>
    public event Action<TerrainChunk>? OnChunkInit;

    /// <summary>
    ///     区块被释放事件
    /// </summary>
    public event Action<TerrainChunk>? OnChunkDiscard;

    /// <summary>
    ///     地形更新进度完成事件
    /// </summary>
    public event Action? UpdateProgressFinished;

    public event Action<TerrainChunk>? ChunkReleasing;

    public event Action<TerrainChunk, ulong>? ChunkAllocationRenewed;

    public event Action<TerrainChunk, bool>? ChunkDerivedDataInvalidated;

    public event Action? DerivedEnvironmentChanged;

    /// <summary>
    ///     初始化 TerrainUpdater 实例
    /// </summary>
    /// <param name="subsystemTerrain">地形子系统</param>
    public TerrainUpdater(SubsystemTerrain subsystemTerrain)
    {
        _subsystemTerrain = subsystemTerrain;
        _subsystemGameInfo = _subsystemTerrain.Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemSky = _subsystemTerrain.Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemBlockBehaviors = _subsystemTerrain.Project.FindSubsystem<SubsystemBlockBehaviors>(true)!;
        _terrain = subsystemTerrain.Terrain;
        _updateParameters.Chunks = [];
        _updateParameters.Locations = new Dictionary<int, TerrainUpdateLocation>();
        _updateParameters.EffectiveLocations = [];
        _threadUpdateParameters.Chunks = [];
        _threadUpdateParameters.Locations = new Dictionary<int, TerrainUpdateLocation>();
        _threadUpdateParameters.EffectiveLocations = [];
        if (_subsystemTerrain.ContentRole == TerrainContentRole.Authority)
        {
            ServerChunkDistribution = new ServerChunkDistributionScheduler(
                _subsystemTerrain.Project,
                _subsystemTerrain.ChunkContentAuthority ??
                throw new InvalidOperationException("Authoritative terrain requires a content authority.")
            );
        }

        SetUpdateLocation(-1, Vector2.Zero, 4, 4);
        _task = Task.Factory.StartNew(
            ThreadUpdateFunction,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
        UnpauseUpdateThread();
        UpdateEvent.Set();
    }

    /// <summary>
    ///     释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        foreach (var preparation in _spawnPreparations.Values) { preparation.Invalidate(); }

        _spawnPreparations.Clear();
        _quitUpdateThread = true;
        UnpauseUpdateThread();
        UpdateEvent.Set();
        _task.GetAwaiter().GetResult();

        ServerChunkDistribution?.Dispose();

        _pauseEvent.Dispose();
        UpdateEvent.Dispose();
    }

    /// <summary>
    ///     请求在当前帧同步更新地形
    /// </summary>
    public void RequestSynchronousUpdate()
    {
        Interlocked.Increment(ref _synchronousUpdateRevision);
    }

    /// <summary>
    ///     设置指定位置的最后更新中心点
    /// </summary>
    /// <param name="locationIndex">位置索引</param>
    /// <param name="value">中心点坐标</param>
    public void SetLastChunksUpdateCenter(int locationIndex, Vector2? value)
    {
        if (!_updateParameters.Locations.TryGetValue(locationIndex, out var location))
        {
            return;
        }

        location.LastChunksUpdateCenter = value;
        _pendingLocations[locationIndex] = location;
    }

    /// <summary>
    ///     设置更新位置的属性
    /// </summary>
    /// <remarks>
    ///     以待交接的位置变更优先于 <see cref="_updateParameters" /> 中已应用的位置，
    ///     如果参数发生变化或位置移动超过阈值，则更新并加入待处理列表
    /// </remarks>
    /// <param name="locationIndex">位置索引</param>
    /// <param name="center">位置中心坐标</param>
    /// <param name="visibilityDistance">可视距离</param>
    /// <param name="contentDistance">内容加载距离</param>
    public void SetUpdateLocation(int locationIndex, Vector2 center, float visibilityDistance, float contentDistance)
    {
        contentDistance = MathUtils.Max(contentDistance, visibilityDistance);
        var location = ResolveUpdateLocation(locationIndex, _updateParameters.Locations, _pendingLocations);
        if (contentDistance.CloseTo(location.ContentDistance) &&
            visibilityDistance.CloseTo(location.VisibilityDistance) &&
            location.LastChunksUpdateCenter.HasValue &&
            !(Vector2.DistanceSquared(center, location.LastChunksUpdateCenter.Value) > 64f))
        {
            return;
        }

        location.Center = center;
        location.VisibilityDistance = visibilityDistance;
        location.ContentDistance = contentDistance;
        location.LastChunksUpdateCenter = center;
        _pendingLocations[locationIndex] = location;
        if (_subsystemTerrain.UsesClientChunkContent)
        {
            NetworkSender.SendToServer(
                WorldRuntimeContext.For(_subsystemTerrain.Project),
                new CharacterUpdateLocationPackage { UpdateLocation = location }
            );
        }
    }

    /// <summary>
    ///     移除指定索引的更新位置
    /// </summary>
    /// <param name="locationIndex">位置索引</param>
    public void RemoveUpdateLocation(int locationIndex)
    {
        _pendingLocations[locationIndex] = null;
    }

    internal static TerrainUpdateLocation ResolveUpdateLocation(
        int locationIndex,
        IReadOnlyDictionary<int, TerrainUpdateLocation> current,
        IReadOnlyDictionary<int, TerrainUpdateLocation?> pending
    ) =>
        pending.TryGetValue(locationIndex, out var queued)
            ? queued.GetValueOrDefault()
            : current.GetValueOrDefault(locationIndex);

    /// <summary>
    ///     获取指定位置的区块更新进度
    /// </summary>
    /// <param name="locationIndex">位置索引</param>
    /// <param name="visibilityDistance">可视距离</param>
    /// <param name="contentDistance">内容加载距离</param>
    /// <returns>更新进度，范围 0.0~1.0</returns>
    public float GetUpdateProgress(int locationIndex, float visibilityDistance, float contentDistance) =>
        _updateParameters.Locations.TryGetValue(locationIndex, out var location)
            ? GetUpdateProgress(location, visibilityDistance, contentDistance)
            : 0f;

    private float GetUpdateProgress(TerrainUpdateLocation location, float visibilityDistance, float contentDistance)
    {
        var validChunkCount = 0;
        var invalidChunkCount = 0;
        visibilityDistance = MathUtils.Max(
            MathUtils.Min(visibilityDistance, location.VisibilityDistance) - 8f - 0.1f,
            0f
        );

        contentDistance = MathUtils.Max(
            MathUtils.Min(contentDistance, location.ContentDistance) - 8f - 0.1f,
            0f
        );

        var visibilityDistanceSqr = MathUtils.Sqr(visibilityDistance);
        var contentDistanceSqr = MathUtils.Sqr(contentDistance);

        var distanceMax = MathUtils.Max(visibilityDistance, contentDistance);

        var chunkLocationMin = Terrain.ToChunk(location.Center - new Vector2(distanceMax));
        var chunkLocationMax = Terrain.ToChunk(location.Center + new Vector2(distanceMax));

        for (var i = chunkLocationMin.X; i <= chunkLocationMax.X; i++)
        {
            for (var j = chunkLocationMin.Y; j <= chunkLocationMax.Y; j++)
            {
                var chunk = _terrain.GetChunkAtCoords(i, j);
                var distanceSqrFromChunkToLocation = Vector2.DistanceSquared(
                    v2: new Vector2((i + 0.5f) * 16f, (j + 0.5f) * 16f),
                    v1: location.Center
                );
                if (distanceSqrFromChunkToLocation <= visibilityDistanceSqr)
                {
                    if (chunk == null ||
                        chunk.MainThreadState < TerrainChunkState.Ready)
                    {
                        invalidChunkCount++;
                    }
                    else
                    {
                        validChunkCount++;
                    }
                }
                else if (distanceSqrFromChunkToLocation <= contentDistanceSqr)
                {
                    if (chunk == null || chunk.MainThreadState < TerrainChunkState.InvalidLight)
                    {
                        invalidChunkCount++;
                    }
                    else
                    {
                        validChunkCount++;
                    }
                }
            }
        }

        if (invalidChunkCount > 0)
        {
            return validChunkCount / (float)(invalidChunkCount + validChunkCount);
        }

        UpdateProgressFinished?.Invoke();
        return 1f;
    }

    /// <summary>
    ///     主更新方法，每帧调用
    /// </summary>
    /// <remarks>
    ///     处理光照变化、季节变化、多线程/单线程更新切换、
    ///     位置更新同步以及区块状态管理等
    /// </remarks>
    public void Update()
    {
        TryDrainReceivedChunkContents();
        ServerChunkDistribution?.Update();

        // 如果光照变化，降级所有的区块到 InvalidLight 状态
        if (_subsystemSky.SkyLightValue != _lastSkylightValue)
        {
            _lastSkylightValue = _subsystemSky.SkyLightValue;
            DowngradeAllChunksState(TerrainChunkState.InvalidLight, false);
        }

        // 温湿度改变只重建客户端网格，不降低 CPU 地形就绪状态。
        if (Time.PeriodicEvent(1, 0.0))
        {
            var temperature =
                (int)MathUtils.Round(_temperatureCurve.Sample(_subsystemGameInfo.WorldSettings.TimeOfYear));
            var humidity = (int)MathUtils.Round(_humidityCurve.Sample(_subsystemGameInfo.WorldSettings.TimeOfYear));
            if (temperature != _terrain.SeasonTemperature || humidity != _terrain.SeasonHumidity)
            {
                _terrain.SeasonTemperature = temperature;
                _terrain.SeasonHumidity = humidity;
                DerivedEnvironmentChanged?.Invoke();
            }
        }

        // 是否有需要处理的 pendingLocations
        if (_pendingLocations.Count > 0 || _preparationsChanged)
        {
            // 给当前短步骤一个有界的交接窗口，避免一次竞争就让后台停到下一帧。
            // 较长步骤仍安全地延后处理，不改变地形修改的互斥边界。
            if (TryPauseUpdateThread(_pauseEvent, UpdateEvent))
            {
                // 恢复更新线程，但是因为 AutoResetEvent 令牌被消耗，更新线程阻塞
                _pauseEvent.Set();
                try
                {
                    foreach (var pendingLocation in _pendingLocations)
                    {
                        if (pendingLocation.Value.HasValue)
                        {
                            _updateParameters.Locations[pendingLocation.Key] = pendingLocation.Value.Value;
                        }
                        else
                        {
                            _updateParameters.Locations.Remove(pendingLocation.Key);
                        }
                    }

                    // 加载或卸载区块
                    _updateParameters.EffectiveLocations = CaptureEffectiveLocations();
                    var allocationResult = AllocateAndFreeChunks(_updateParameters.EffectiveLocations);
                    if (allocationResult.Changed)
                    {
                        _updateParameters.Chunks = _terrain.AllocatedChunks;
                    }

                    // 存档队列满时保留位置变更，后续帧继续卸载旧区块并分配新区块。
                    if (CanCompleteLocationUpdate(allocationResult.RetryRequired))
                    {
                        _pendingLocations.Clear();
                        _preparationsChanged = false;
                    }
                }
                finally
                {
                    // 重新设置 AutoResetEvent 令牌，让阻塞的更新线程恢复执行
                    UpdateEvent.Set();
                }
            }
            else
            {
                LocationHandoffTimeouts++;
            }
        }

        // 位置更新不能阻止主、后台区块状态交换，否则已经生成的几何会长期保持不可见。
        if (_updateParametersLock.TryEnter())
        {
            try
            {
                if (SendReceiveChunkStates())
                {
                    UnpauseUpdateThread();
                }
            }
            finally
            {
                _updateParametersLock.Exit();
            }
        }

        var activeLocations = _updateParameters.Locations.Values.ToArray();
        foreach (var terrainChunk in _terrain.AllocatedChunks)
        {
            if (terrainChunk.MainThreadState < TerrainChunkState.Ready || terrainChunk.AreBehaviorsNotified)
            {
                continue;
            }

            terrainChunk.AreBehaviorsNotified = true;
            NotifyBlockBehaviors(terrainChunk);
        }

        LogStalledNetworkChunks();

        if (!_subsystemTerrain.UsesChunkContentTransport) { return; }

        var transport = _subsystemTerrain.ChunkContentTransport ??
                        throw new InvalidOperationException(
                            "Client terrain requests require a chunk content transport."
                        );
        if (RequestChunkContents(_terrain.AllocatedChunks, activeLocations, transport, Time.RealTime) > 0)
        {
            UnpauseUpdateThread();
        }
    }

    internal static int RequestChunkContents(
        IEnumerable<TerrainChunk> chunks,
        TerrainUpdateLocation[] locations,
        IChunkContentTransport transport,
        double now
    )
    {
        if (locations.Length == 0) { return 0; }

        var selected = chunks.Where(chunk => (!chunk.IsLoaded || chunk.NeedsContentSnapshot) &&
                                             IsChunkInRange(chunk.Center, locations) &&
                                             ShouldRequestNetworkChunk(chunk, now)
            )
            .OrderBy(chunk => locations.Min(location => Vector2.DistanceSquared(chunk.Center, location.Center)))
            .ThenBy(chunk => chunk.Coords.X).ThenBy(chunk => chunk.Coords.Y)
            .Take(TerrainChunksRequestPackage.MaximumRequests).ToArray();
        if (selected.Length == 0) { return 0; }

        transport.Request(
            selected.Select(chunk => new ChunkContentRequest(
                    new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration),
                    chunk.NetworkContentVersion
                )
            ).ToArray()
        );
        foreach (var chunk in selected)
        {
            chunk.IsRequested = true;
            chunk.NetworkRequestTime = now;
            chunk.NetworkRequestAttempts++;
        }

        return selected.Length;
    }

    private bool TryDrainReceivedChunkContents()
    {
        var transport = _subsystemTerrain.ChunkContentTransport;
        var coordinator = _subsystemTerrain.ClientChunkContents;
        if (transport == null || coordinator == null)
        {
            return true;
        }

        // Installing authoritative arrays and resetting their derived state must not race
        // a lighting or geometry step. Leave deliveries queued when the worker owns the token.

        _pauseEvent.Reset();
        if (!UpdateEvent.WaitOne(0))
        {
            return false;
        }

        _pauseEvent.Set();
        try
        {
            var deltaCoordinator = _subsystemTerrain.ClientTerrainDeltas ??
                                   throw new InvalidOperationException("Client content requires a delta coordinator.");
            _clientContentApplication ??= new ClientChunkContentApplication(transport, coordinator, deltaCoordinator);
            _clientContentApplication.Pump();

            return true;
        }
        finally
        {
            UpdateEvent.Set();
        }
    }

    public static bool ShouldRequestNetworkChunk(TerrainChunk chunk, double now)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        return !chunk.IsRequested ||
               now < chunk.NetworkRequestTime ||
               now - chunk.NetworkRequestTime >= NetworkChunkRequestRetrySeconds;
    }

    private void LogStalledNetworkChunks()
    {
        if (_subsystemTerrain.ContentRole != TerrainContentRole.Replica)
        {
            return;
        }

        var now = Time.RealTime;
        if (now - _lastNetworkChunkDiagnosticTime < NetworkChunkRequestRetrySeconds)
        {
            return;
        }

        _lastNetworkChunkDiagnosticTime = now;
        var activeLocations = _updateParameters.Locations.Values.ToArray();
        var stalled = _terrain.AllocatedChunks
            .Where(chunk => IsChunkInRange(chunk.Center, activeLocations))
            .Where(chunk => !chunk.IsLoaded &&
                            chunk.IsRequested &&
                            now - chunk.NetworkRequestTime >= NetworkChunkRequestRetrySeconds
            )
            .ToArray();
        if (stalled.Length == 0)
        {
            return;
        }

        Log.Warning($"Terrain chunk stalled summary: count={stalled.Length}.");
    }

    /// <summary>Synchronously converges CPU contents and lighting under the terrain worker token.</summary>
    public void EnsureCpuReady(IReadOnlyList<TerrainChunk> chunks)
    {
        UpdateEvent.WaitOne();
        try
        {
            SendReceiveChunkStates();
            SendReceiveChunkStatesThread();
            foreach (var chunk in chunks)
            {
                if (!CanSynchronouslyUpdateChunk(_subsystemTerrain.ContentRole, chunk)) { continue; }

                while (chunk.WorkerState < TerrainChunkState.Ready)
                {
                    var dependency = ClientDerivedTerrainPolicy.FindPendingLightingDependency(
                        _terrain,
                        _subsystemTerrain.ContentRole,
                        chunk
                    );
                    UpdateChunkSingleStep(dependency ?? chunk, _subsystemSky.SkyLightValue);
                }
            }

            SendReceiveChunkStatesThread();
            SendReceiveChunkStates();
        }
        finally
        {
            UpdateEvent.Set();
        }
    }

    internal static void ResetInstalledChunkLighting(Terrain terrain, TerrainChunk target)
    {
        target.SetMainThreadState(TerrainChunkState.InvalidLight);
        target.SetWorkerState(TerrainChunkState.InvalidLight);
        target.ResetStateExchange();
        for (var x = -1; x <= 1; x++)
        {
            for (var z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0) { continue; }

                var neighbor = terrain.GetChunkAtCoords(target.Coords.X + x, target.Coords.Y + z);
                if (neighbor is not { IsLoaded: true }) { continue; }

                TerrainChunkStateExchange.RequestDowngrade(neighbor, TerrainChunkState.InvalidLight);
            }
        }
    }

    internal static bool CanSynchronouslyUpdateChunk(TerrainContentRole role, TerrainChunk chunk)
    {
        return role != TerrainContentRole.Replica || chunk.IsLoaded;
    }

    /// <summary>
    ///     降级指定坐标周围区块的状态
    /// </summary>
    /// <param name="coordinates">中心区块坐标</param>
    /// <param name="radius">影响半径（区块数）</param>
    /// <param name="state">目标降级状态</param>
    /// <param name="forceDerivedDataRegeneration">是否丢弃派生数据缓存</param>
    public void DowngradeChunkNeighborhoodState(
        Point2 coordinates,
        int radius,
        TerrainChunkState state,
        bool forceDerivedDataRegeneration
    )
    {
        for (var i = -radius; i <= radius; i++)
        {
            for (var j = -radius; j <= radius; j++)
            {
                var chunkAtCoords = _terrain.GetChunkAtCoords(coordinates.X + i, coordinates.Y + j);
                if (chunkAtCoords == null)
                {
                    continue;
                }

                if (chunkAtCoords.MainThreadState > state)
                {
                    chunkAtCoords.SetMainThreadState(state);
                }

                ChunkDerivedDataInvalidated?.Invoke(chunkAtCoords, forceDerivedDataRegeneration);
                TerrainChunkStateExchange.RequestDowngrade(chunkAtCoords, state);
            }
        }
    }

    /// <summary>
    ///     降级所有已分配区块的状态
    /// </summary>
    /// <param name="state">目标降级状态</param>
    /// <param name="forceDerivedDataRegeneration">是否丢弃派生数据缓存</param>
    public void DowngradeAllChunksState(TerrainChunkState state, bool forceDerivedDataRegeneration)
    {
        var allocatedChunks = _terrain.AllocatedChunks;
        foreach (var terrainChunk in allocatedChunks)
        {
            if (terrainChunk.MainThreadState > state)
            {
                terrainChunk.SetMainThreadState(state);
            }

            ChunkDerivedDataInvalidated?.Invoke(terrainChunk, forceDerivedDataRegeneration);
            TerrainChunkStateExchange.RequestDowngrade(terrainChunk, state);
        }
    }

    internal static ulong RefreshRetainedChunk(
        Terrain terrain,
        TerrainChunk chunk,
        IChunkContentTransport transport,
        ClientTerrainDeltaCoordinator deltas
    )
    {
        transport.Discard(new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration));
        deltas.Discard(chunk.Coords);
        var previousGeneration = chunk.AllocationGeneration;
        terrain.RenewChunkAllocation(chunk);
        // Cached cells and uploaded geometry remain usable while this new lease gets a baseline.
        chunk.NeedsContentValidation = true;
        chunk.NeedsContentSnapshot = true;
        chunk.IsRequested = false;
        chunk.NetworkRequestTime = 0;
        chunk.NetworkRequestAttempts = 0;
        return previousGeneration;
    }

    /// <summary>
    ///     检查区块中心是否在任一更新位置的范围内
    /// </summary>
    /// <param name="chunkCenter">区块中心坐标</param>
    /// <param name="locations">更新位置数组</param>
    /// <returns>如果在任一位置的 ContentDistance 范围内则返回 true</returns>
    internal static bool IsChunkInRange(
        Vector2 chunkCenter,
        TerrainUpdateLocation[] locations,
        float distanceMargin = 0f
    )
    {
        for (var i = 0; i < locations.Length; i++)
        {
            var distance = Vector2.DistanceSquared(locations[i].Center, chunkCenter);
            var content = MathUtils.Sqr(locations[i].ContentDistance + distanceMargin);
            if (distance <= content)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     根据更新位置分配和释放区块
    /// </summary>
    /// <param name="locations">更新位置数组</param>
    /// <returns>区块是否发生变化，以及是否需要在后续帧重试</returns>
    internal static bool ShouldDiscardChunk(
        TerrainChunk chunk,
        TerrainUpdateLocation[] locations,
        float retentionMargin,
        IReadOnlySet<Point2> retentionEvictions
    ) =>
        !IsChunkInRange(chunk.Center, locations, retentionMargin) || retentionEvictions.Contains(chunk.Coords);

    private ChunkAllocationResult AllocateAndFreeChunks(TerrainUpdateLocation[] locations)
    {
        var result = false;
        var hasDeferredChunkDiscards = false;
        var allocatedChunks = _terrain.AllocatedChunks;
        HashSet<Point2> retentionEvictions = [];
        if (_subsystemTerrain.UsesClientChunkContent)
        {
            var access = ++_remoteChunkAccessClock;
            foreach (var chunk in allocatedChunks)
            {
                if (!IsChunkInRange(chunk.Center, locations))
                {
                    _inactiveClientChunks.Add(chunk.Coords);
                    continue;
                }

                if (_inactiveClientChunks.Remove(chunk.Coords))
                {
                    var previousGeneration = RefreshRetainedChunk(
                        _terrain,
                        chunk,
                        _subsystemTerrain.ChunkContentTransport!,
                        _subsystemTerrain.ClientTerrainDeltas!
                    );
                    ChunkAllocationRenewed?.Invoke(chunk, previousGeneration);
                }

                _remoteChunkLastAccess[chunk.Coords] = access;
            }

            var retained = allocatedChunks
                .Where(chunk => !IsChunkInRange(chunk.Center, locations))
                .Where(chunk => IsChunkInRange(
                        chunk.Center,
                        locations,
                        NetworkTerrainPolicy.ClientChunkRetentionMargin
                    )
                )
                .Select(chunk => (chunk.Coords, _remoteChunkLastAccess.GetValueOrDefault(chunk.Coords)))
                .ToArray();
            retentionEvictions = SelectRetainedChunkCoordsToEvict(
                    retained,
                    NetworkTerrainPolicy.ClientRetainedChunkCapacity
                )
                .ToHashSet();
        }

        foreach (var terrainChunk in allocatedChunks)
        {
            var retentionMargin = _subsystemTerrain.UsesClientChunkContent
                ? NetworkTerrainPolicy.ClientChunkRetentionMargin
                : 0f;
            if (!ShouldDiscardChunk(terrainChunk, locations, retentionMargin, retentionEvictions))
            {
                continue;
            }

            var saveCoordinator = _subsystemTerrain.ContentRole == TerrainContentRole.Authority
                ? _subsystemTerrain.TerrainSaveCoordinator
                : null;
            if (saveCoordinator != null &&
                TerrainSaveCoordinator.RequiresSave(terrainChunk) &&
                !saveCoordinator.CanAcceptUnloadSnapshot)
            {
                hasDeferredChunkDiscards = true;
                continue;
            }

            OnChunkDiscard?.Invoke(terrainChunk);
            foreach (var blockBehavior in _subsystemBlockBehaviors.BlockBehaviors)
            {
                blockBehavior.OnChunkDiscarding(terrainChunk);
            }

            WorldRuleHooks.For(_subsystemTerrain.Project).Invoke(
                new TerrainChunkDiscardingContext(
                    _subsystemTerrain,
                    terrainChunk
                )
            );
            if (!WorldRuntimeContext.IsProjectActive(_subsystemTerrain.Project))
            {
                return new ChunkAllocationResult(result, false);
            }

            ServerChunkDistribution?.OnChunkRemoved(terrainChunk);
            if (saveCoordinator != null)
            {
                if (!saveCoordinator.TryQueueChunkForUnload(terrainChunk))
                {
                    hasDeferredChunkDiscards = true;
                    continue;
                }
            }
            else if (_subsystemTerrain.ContentRole == TerrainContentRole.Authority)
            {
                (_subsystemTerrain.TerrainSerializer ??
                 throw new InvalidOperationException("Authority terrain requires a disk serializer."))
                    .SaveChunk(terrainChunk);
            }

            result = true;
            _subsystemTerrain.ClientTerrainDeltas?.Discard(terrainChunk.Coords);
            _subsystemTerrain.ChunkContentTransport?.Discard(
                new ChunkAllocationId(terrainChunk.Coords, terrainChunk.AllocationGeneration)
            );
            _inactiveClientChunks.Remove(terrainChunk.Coords);
            ChunkReleasing?.Invoke(terrainChunk);
            _terrain.FreeChunk(terrainChunk);
            _remoteChunkLastAccess.Remove(terrainChunk.Coords);
        }

        if (hasDeferredChunkDiscards)
        {
            DeferredAllocationPasses++;
            return new ChunkAllocationResult(result, true);
        }

        for (var j = 0; j < locations.Length; j++)
        {
            var point = Terrain.ToChunk(locations[j].Center - new Vector2(locations[j].ContentDistance));
            var point2 = Terrain.ToChunk(locations[j].Center + new Vector2(locations[j].ContentDistance));
            for (var k = point.X; k <= point2.X; k++)
            {
                for (var l = point.Y; l <= point2.Y; l++)
                {
                    var chunkCenter = new Vector2((k + 0.5f) * 16f, (l + 0.5f) * 16f);
                    var chunkAtCoords = _terrain.GetChunkAtCoords(k, l);
                    if (chunkAtCoords == null)
                    {
                        if (!IsChunkInRange(chunkCenter, locations))
                        {
                            continue;
                        }

                        result = true;
                        _terrain.AllocateChunk(k, l);
                        if (_subsystemTerrain.UsesClientChunkContent)
                        {
                            _remoteChunkLastAccess[new Point2(k, l)] = _remoteChunkAccessClock;
                        }

                        DowngradeChunkNeighborhoodState(new Point2(k, l), 0, TerrainChunkState.NotLoaded, false);
                        DowngradeChunkNeighborhoodState(new Point2(k, l), 1, TerrainChunkState.InvalidLight, false);
                    }
                    else if (chunkAtCoords.Coords.X != k || chunkAtCoords.Coords.Y != l)
                    {
                        Log.Error("Chunk wraparound detected at {0}", chunkAtCoords.Coords);
                    }
                }
            }
        }

        return new ChunkAllocationResult(result, false);
    }

    internal static IReadOnlyList<Point2> SelectRetainedChunkCoordsToEvict(
        IEnumerable<(Point2 Coords, long LastAccess)> retainedChunks,
        int capacity
    )
    {
        ArgumentNullException.ThrowIfNull(retainedChunks);
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        return retainedChunks
            .OrderByDescending(item => item.LastAccess)
            .ThenBy(item => item.Coords.X)
            .ThenBy(item => item.Coords.Y)
            .Skip(capacity)
            .Select(item => item.Coords)
            .ToArray();
    }

    private readonly record struct ChunkAllocationResult(bool Changed, bool RetryRequired);

    internal static bool CanCompleteLocationUpdate(bool retryRequired)
    {
        return !retryRequired;
    }

    /// <summary>
    ///     在主线程中处理区块状态的升降级同步
    /// </summary>
    /// <remarks>
    ///     接收后台线程发布的状态；待处理降级始终优先。
    /// </remarks>
    /// <returns>如果有区块被降级则返回 true</returns>
    private bool SendReceiveChunkStates()
    {
        return TerrainChunkStateExchange.ReceiveOnMainThread(_updateParameters.Chunks);
    }

    /// <summary>
    ///     在更新线程中处理区块状态的升降级同步
    /// </summary>
    /// <remarks>
    ///     接收主线程降级请求，或发布后台线程的当前状态。
    /// </remarks>
    private void SendReceiveChunkStatesThread()
    {
        TerrainChunkStateExchange.ExchangeOnWorkerThread(_threadUpdateParameters.Chunks);
    }

    internal static bool TryPauseUpdateThread(
        ManualResetEvent pauseEvent,
        AutoResetEvent updateEvent,
        int millisecondsTimeout = 1
    )
    {
        pauseEvent.Reset();
        return updateEvent.WaitOne(millisecondsTimeout);
    }

    /// <summary>
    ///     更新线程的主循环逻辑
    /// </summary>
    /// <remarks>
    ///     在线程中循环调用 <see cref="ProcessNextChunkUpdate" /> 进行地形更新，
    ///     使用 <see cref="_pauseEvent" /> 和 <see cref="UpdateEvent" /> 进行线程同步
    /// </remarks>
    private void ThreadUpdateFunction()
    {
        while (!_quitUpdateThread)
        {
            // 等待恢复更新的许可，该许可是手动更新的，用于程序控制更新线程的暂停与恢复
            _pauseEvent.WaitOne();
            // 获取并消耗更新的令牌，该令牌也可能被主线程获取消耗，此时，更新线程将在此阻塞，直到发放新的令牌
            UpdateEvent.WaitOne();
            try
            {
                if (ProcessNextChunkUpdate())
                {
                    lock (_unpauseLock)
                    {
                        if (!_unpauseUpdateThread)
                        {
                            _pauseEvent.Reset();
                        }

                        _unpauseUpdateThread = false;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"Terrain update worker failed. {e}");
            }
            finally
            {
                // 发放新的更新令牌
                UpdateEvent.Set();
            }
        }
    }

    /// <summary>
    ///     处理一个地形更新步骤
    /// </summary>
    /// <remarks>
    ///     查找并更新一个最佳区块的状态，如果所有区块都已更新完毕则返回 true。
    ///     该方法只由地形更新线程调用
    /// </remarks>
    /// <returns>如果所有区块都已完成更新则返回 true，否则返回 false</returns>
    private bool ProcessNextChunkUpdate()
    {
        lock (_updateParametersLock)
        {
            _threadUpdateParameters = _updateParameters;
            SendReceiveChunkStatesThread();
        }

        // Content delivery must not wait for all nearby lighting work to converge.
        var preferContent = _subsystemTerrain.ContentRole == TerrainContentRole.Authority &&
                            (++_chunkSchedulingStep & 3) != 0;
        var locations = _threadUpdateParameters.EffectiveLocations;
        var terrainChunk = SelectChunkToUpdate(
            _threadUpdateParameters.Chunks,
            locations,
            _subsystemTerrain.ContentRole,
            preferContent,
            out _
        );
        var derivedChunk = _derivedWork?.SelectChunk(
            _threadUpdateParameters.Chunks,
            _threadUpdateParameters.Locations.Values
        );
        if (PreferDerivedWork(terrainChunk, derivedChunk, locations))
        {
            var dependency = _derivedWork!.GetCpuDependency(derivedChunk!);
            if (dependency != null)
            {
                UpdateChunkSingleStep(dependency, _subsystemSky.SkyLightValue);
            }
            else
            {
                _derivedWork.UpdateChunk(derivedChunk!);
            }

            return false;
        }

        if (terrainChunk == null) { return true; }

        var lightingDependency = ClientDerivedTerrainPolicy.FindPendingLightingDependency(
            _terrain,
            _subsystemTerrain.ContentRole,
            terrainChunk
        );
        UpdateChunkSingleStep(lightingDependency ?? terrainChunk, _subsystemSky.SkyLightValue);
        return false;
    }

    internal static bool PreferDerivedWork(
        TerrainChunk? cpuChunk,
        TerrainChunk? derivedChunk,
        IEnumerable<TerrainUpdateLocation> locations
    )
    {
        if (derivedChunk == null) { return false; }

        if (cpuChunk == null) { return true; }

        var cpuDistance = float.MaxValue;
        var derivedDistance = float.MaxValue;
        foreach (var location in locations)
        {
            cpuDistance = MathUtils.Min(cpuDistance, Vector2.DistanceSquared(location.Center, cpuChunk.Center));
            derivedDistance = MathUtils.Min(
                derivedDistance,
                Vector2.DistanceSquared(location.Center, derivedChunk.Center)
            );
        }

        return derivedDistance <= cpuDistance;
    }

    internal static TerrainChunk? SelectChunkToUpdate(
        TerrainChunk[] chunks,
        IEnumerable<TerrainUpdateLocation> locations,
        TerrainContentRole role,
        bool preferContent,
        out TerrainChunkState desiredState
    )
    {
        // 初始的距离（平方，下面都用距离替代）的最大限制
        var maxDistanceSquared = 3.40282347E+38f;
        var bestPriority = 1;

        TerrainChunk? result = null;
        desiredState = TerrainChunkState.NotLoaded;

        // 逐渐收敛距离的最大限制，找到最合适的更新区块和预期的状态
        foreach (var terrainChunk in chunks)
        {
            // A client cannot advance a chunk until its contents arrive from the server.
            // Selecting a nearby NotLoaded chunk repeatedly would otherwise starve loaded
            // chunks (for example, chunks waiting for lighting and geometry) under packet loss.
            if (!CanBackgroundUpdateChunk(role, terrainChunk))
            {
                continue;
            }

            // 跳过 CPU 已就绪的区块
            if (terrainChunk.WorkerState >= TerrainChunkState.Ready)
            {
                continue;
            }

            var priority = preferContent &&
                           role == TerrainContentRole.Authority &&
                           terrainChunk.WorkerState < TerrainChunkState.InvalidLight
                ? 0
                : 1;
            if (priority > bestPriority)
            {
                continue;
            }

            foreach (var location in locations)
            {
                // 位置与区块中心的距离
                var distanceSquared = Vector2.DistanceSquared(location.Center, terrainChunk.Center);
                // 距离大于最大限制的区块，跳过
                if (priority == bestPriority && !(distanceSquared < maxDistanceSquared))
                {
                    continue;
                }

                // 距离小于位置 location 的可视范围，区块的预期状态设置为 Valid 将最大限制设置为区块与位置距离
                if (distanceSquared <= MathUtils.Sqr(location.VisibilityDistance))
                {
                    desiredState = TerrainChunkState.Ready;
                    bestPriority = priority;
                    maxDistanceSquared = distanceSquared;
                    result = terrainChunk;
                }
                // 否则，如果区块的线程状态尚未 CPU 就绪, 并且距离小于位置的 ContentDistance 的距离，
                // 则预期的区块状态为 CPU Ready, 更新最大限制为当前距离
                else if (terrainChunk.WorkerState < TerrainChunkState.Ready &&
                         distanceSquared <= MathUtils.Sqr(location.ContentDistance))
                {
                    desiredState = TerrainChunkState.Ready;
                    bestPriority = priority;
                    maxDistanceSquared = distanceSquared;
                    result = terrainChunk;
                }
            }
        }

        return result;
    }

    internal static bool CanBackgroundUpdateChunk(TerrainContentRole role, TerrainChunk chunk) =>
        role != TerrainContentRole.Replica || chunk.WorkerState != TerrainChunkState.NotLoaded;

    public void RequestDerivedWorkUpdate() => UnpauseUpdateThread();

    public void AttachDerivedWork(ITerrainDerivedWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        UpdateEvent.WaitOne();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_derivedWork != null || _subsystemTerrain.ContentRole != TerrainContentRole.Replica)
            {
                throw new InvalidOperationException("Derived work requires an unbound replica terrain updater.");
            }

            _derivedWork = work;
        }
        finally { UpdateEvent.Set(); }

        UnpauseUpdateThread();
    }

    public void DetachDerivedWork(ITerrainDerivedWork work)
    {
        UpdateEvent.WaitOne();
        try
        {
            if (ReferenceEquals(_derivedWork, work)) { _derivedWork = null; }
        }
        finally { UpdateEvent.Set(); }
    }

    public void NotifyNetworkChunkLoaded()
    {
        UnpauseUpdateThread();
    }

    public void OnNetworkChunkContentInstalled(TerrainChunk target)
    {
        ResetInstalledChunkLighting(_terrain, target);
        for (var x = -1; x <= 1; x++)
        {
            for (var z = -1; z <= 1; z++)
            {
                if (_terrain.GetChunkAtCoords(target.Coords.X + x, target.Coords.Y + z) is { } chunk)
                {
                    ChunkDerivedDataInvalidated?.Invoke(chunk, true);
                }
            }
        }

        target.IsRequested = false;
        target.NetworkRequestTime = 0.0;
        target.NetworkContentReceiveTime = Time.RealTime;
        NotifyNetworkChunkLoaded();
    }

    /// <summary>
    ///     单步更新区块的状态
    /// </summary>
    /// <param name="chunk">要更新的区块</param>
    /// <param name="skylightValue">当前天空光照值</param>
    /// <remarks>
    ///     根据区块当前状态执行相应的处理：加载数据、生成内容和计算光照
    /// </remarks>
    private void UpdateChunkSingleStep(TerrainChunk chunk, int skylightValue)
    {
        if (chunk.WorkerState <= TerrainChunkState.InvalidContents4)
        {
            var generation = _subsystemTerrain.AuthoritativeChunkGeneration;
            if (generation == null)
            {
                throw new InvalidOperationException(
                    $"Client attempted authoritative terrain generation at {chunk.Coords} ({chunk.WorkerState})."
                );
            }

            generation.TryAdvance(chunk);
            return;
        }

        switch (chunk.WorkerState)
        {
            case TerrainChunkState.InvalidLight:
            {
                GenerateChunkSunLightAndHeight(chunk, skylightValue, _subsystemTerrain.Catalog);
                chunk.SetWorkerState(TerrainChunkState.InvalidPropagatedLight);
                break;
            }
            case TerrainChunkState.InvalidPropagatedLight:
            {
                var dependency = ClientDerivedTerrainPolicy.FindPendingLightingDependency(
                    _terrain,
                    _subsystemTerrain.ContentRole,
                    chunk
                );
                if (dependency != null)
                {
                    UpdateChunkSingleStep(dependency, skylightValue);
                    break;
                }

                _lightSources.Clear();
                GenerateChunkLightSources(chunk);
                GenerateChunkEdgeLightSources(chunk, 0);
                GenerateChunkEdgeLightSources(chunk, 1);
                GenerateChunkEdgeLightSources(chunk, 2);
                GenerateChunkEdgeLightSources(chunk, 3);

                PropagateLight();
                chunk.SetWorkerState(TerrainChunkState.Ready);
                break;
            }
        }
    }

    /// <summary>
    ///     生成区块的天空光照和高度信息
    /// </summary>
    /// <param name="chunk">目标区块</param>
    /// <param name="skylightValue">天空光照值</param>
    /// <remarks>
    ///     计算每个列的最高不透明方块高度，并填充天空光照
    /// </remarks>
    internal static void GenerateChunkSunLightAndHeight(
        TerrainChunk chunk,
        int skylightValue,
        BlockContentCatalog catalog
    )
    {
        for (var i = 0; i < 16; i++)
        {
            for (var j = 0; j < 16; j++)
            {
                var num = 0;
                var num2 = 256;
                var num4 = 255;
                var num5 = TerrainChunk.CalculateCellIndex(i, 255, j);
                while (num4 >= 0)
                {
                    var cellValueFast = chunk.GetCellValueForGeometry(num5);
                    if (Terrain.ExtractContents(cellValueFast) != 0)
                    {
                        num = num4;
                        break;
                    }

                    cellValueFast = Terrain.ReplaceLight(cellValueFast, skylightValue);
                    chunk.SetCellLightFast(num5, Terrain.ExtractLight(cellValueFast));
                    num4--;
                    num5--;
                }

                num4 = 0;
                num5 = TerrainChunk.CalculateCellIndex(i, 0, j);
                while (num4 <= num + 1)
                {
                    var cellValueFast2 = chunk.GetCellValueForGeometry(num5);
                    var num6 = Terrain.ExtractContents(cellValueFast2);
                    if (catalog.Blocks[num6].Transparent)
                    {
                        num2 = num4;
                        break;
                    }

                    cellValueFast2 = Terrain.ReplaceLight(cellValueFast2, 0);
                    chunk.SetCellLightFast(num5, Terrain.ExtractLight(cellValueFast2));
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
                        var cellValueFast3 = chunk.GetCellValueForGeometry(num5);
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
                        chunk.SetCellLightFast(num5, Terrain.ExtractLight(cellValueFast3));
                        num4--;
                        num5--;
                    }
                }

                var num3 = num4 + 1;
                while (num4 >= num2)
                {
                    var cellValueFast4 = chunk.GetCellValueForGeometry(num5);
                    cellValueFast4 = Terrain.ReplaceLight(cellValueFast4, 0);
                    chunk.SetCellLightFast(num5, Terrain.ExtractLight(cellValueFast4));
                    num4--;
                    num5--;
                }

                chunk.SetTopHeightFast(i, j, num);
                chunk.SetBottomHeightFast(i, j, num2);
                chunk.SetSunlightHeightFast(i, j, num3);
            }
        }
    }

    /// <summary>
    ///     生成区块的光源列表（用于传播光照计算）
    /// </summary>
    /// <param name="chunk">目标区块</param>
    /// <remarks>
    ///     扫描区块内所有发光方块和受邻近区块光照影响的方块
    /// </remarks>
    private void GenerateChunkLightSources(TerrainChunk chunk)
    {
        var blocks = _subsystemTerrain.Catalog.Blocks;
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
                var shaftValueFast = chunkAtCell.GetShaftValueFast(x, z);
                var shaftValueFast2 = chunkAtCell2.GetShaftValueFast(x2, z2);
                var shaftValueFast3 = chunkAtCell3.GetShaftValueFast(x3, z3);
                var shaftValueFast4 = chunkAtCell4.GetShaftValueFast(x4, z4);
                var x5 = Terrain.ExtractSunlightHeight(shaftValueFast);
                var x6 = Terrain.ExtractSunlightHeight(shaftValueFast2);
                var x7 = Terrain.ExtractSunlightHeight(shaftValueFast3);
                var x8 = Terrain.ExtractSunlightHeight(shaftValueFast4);
                var num3 = MathUtils.Min(x5, x6, x7, x8);
                var num4 = bottomHeightFast;
                var num5 = TerrainChunk.CalculateCellIndex(i, bottomHeightFast, j);
                while (num4 <= topHeightFast)
                {
                    var cellValueFast = chunk.GetCellValueForGeometry(num5);
                    var num6 = 0;
                    var block = blocks[Terrain.ExtractContents(cellValueFast)];
                    if (num4 >= num3 && block.Transparent)
                    {
                        var cellLightFast = chunkAtCell.GetCellLightFast(x, num4, z);
                        var cellLightFast2 = chunkAtCell2.GetCellLightFast(x2, num4, z2);
                        var cellLightFast3 = chunkAtCell3.GetCellLightFast(x3, num4, z3);
                        var cellLightFast4 = chunkAtCell4.GetCellLightFast(x4, num4, z4);
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
                        chunk.SetCellLightFast(num5, num6);
                        _lightSources.Add(
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

    /// <summary>
    ///     从相邻区块向目标区块的一条边界引入光源。
    /// </summary>
    /// <param name="chunk">目标区块。</param>
    /// <param name="face">边界方向。</param>
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
            !ClientDerivedTerrainPolicy.CanAdvanceLightingDependency(_subsystemTerrain.ContentRole, neighbor) ||
            neighbor.WorkerState < TerrainChunkState.InvalidPropagatedLight)
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
                var cellValue = chunk.GetCellValueForGeometry(cellIndex + y);
                var block = _subsystemTerrain.Catalog.Blocks[Terrain.ExtractContents(cellValue)];
                if (!block.Transparent)
                {
                    continue;
                }

                var light = Terrain.ExtractLight(neighbor.GetCellValueForGeometry(neighborCellIndex + y)) - 1;
                if (light <= Terrain.ExtractLight(cellValue))
                {
                    continue;
                }

                chunk.SetCellLightFast(cellIndex + y, light);
                if (light > 1)
                {
                    _lightSources.Add(
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

    /// <summary>
    ///     从指定位置传播光源到相邻方块
    /// </summary>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    /// <param name="z">Z坐标</param>
    /// <param name="light">光照强度</param>
    private void PropagateLightSource(int x, int y, int z, int light)
    {
        var chunkAtCell = _terrain.GetChunkAtCell(x, z, false);
        if (chunkAtCell == null)
        {
            return;
        }

        var index = TerrainChunk.CalculateCellIndex(x & 0xF, y, z & 0xF);
        var cellValueFast = chunkAtCell.GetCellValueForGeometry(index);
        var num = Terrain.ExtractContents(cellValueFast);
        var block = _subsystemTerrain.Catalog.Blocks[num];
        if (!block.Transparent)
        {
            return;
        }

        var num2 = light - block.LightAttenuation - 1;
        if (num2 <= Terrain.ExtractLight(cellValueFast))
        {
            return;
        }

        _lightSources.Add(
            new LightSource
            {
                X = x,
                Y = y,
                Z = z,
                Light = num2
            }
        );
        chunkAtCell.SetCellLightFast(index, num2);
    }

    /// <summary>
    ///     传播所有光源的光照
    /// </summary>
    /// <remarks>
    ///     遍历光源列表，向六个方向传播光照（限制最多处理 120000 个光源）
    /// </remarks>
    private void PropagateLight()
    {
        for (var i = 0; i < _lightSources.Count && i < 120000; i++)
        {
            var lightSource = _lightSources.Array[i];
            var light = lightSource.Light;
            if (light <= 1)
            {
                continue;
            }

            PropagateLightSource(lightSource.X - 1, lightSource.Y, lightSource.Z, light);
            PropagateLightSource(lightSource.X + 1, lightSource.Y, lightSource.Z, light);
            if (lightSource.Y > 0)
            {
                PropagateLightSource(lightSource.X, lightSource.Y - 1, lightSource.Z, light);
            }

            if (lightSource.Y < 255)
            {
                PropagateLightSource(lightSource.X, lightSource.Y + 1, lightSource.Z, light);
            }

            PropagateLightSource(lightSource.X, lightSource.Y, lightSource.Z - 1, light);
            PropagateLightSource(lightSource.X, lightSource.Y, lightSource.Z + 1, light);
        }
    }

    /// <summary>
    ///     通知方块行为器区块已初始化
    /// </summary>
    /// <param name="chunk">已初始化的区块</param>
    /// <remarks>
    ///     触发 OnChunkInit 事件，并调用所有方块行为器的 OnChunkInitialized 和 OnBlockGenerated 方法
    /// </remarks>
    private void NotifyBlockBehaviors(TerrainChunk chunk)
    {
        OnChunkInit?.Invoke(chunk);
        foreach (var blockBehavior in _subsystemBlockBehaviors.BlockBehaviors)
        {
            blockBehavior.OnChunkInitialized(chunk);
        }

        var isLoaded = chunk.IsLoaded;
        for (var i = 0; i < 16; i++)
        {
            for (var j = 0; j < 16; j++)
            {
                var x = i + chunk.Origin.X;
                var z = j + chunk.Origin.Y;
                var num = TerrainChunk.CalculateCellIndex(i, 0, j);
                var num2 = 0;
                while (num2 < 255)
                {
                    var cellValueFast = chunk.GetCellValueFast(num);
                    var num3 = Terrain.ExtractContents(cellValueFast);
                    if (num3 != 0)
                    {
                        var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(num3);
                        foreach (var blockBehavior in blockBehaviors)
                        {
                            blockBehavior.OnBlockGenerated(cellValueFast, x, num2, z, isLoaded);
                        }
                    }

                    num2++;
                    num++;
                }
            }
        }

        WorldRuleHooks.For(_subsystemTerrain.Project).Invoke(
            new TerrainChunkInitializedContext(
                _subsystemTerrain,
                chunk
            )
        );
    }

    /// <summary>
    ///     恢复（唤醒）更新线程
    /// </summary>
    private void UnpauseUpdateThread()
    {
        lock (_unpauseLock)
        {
            _unpauseUpdateThread = true;
            _pauseEvent.Set();
        }
    }

    /// <summary>
    ///     地形更新参数结构体
    /// </summary>
    private struct UpdateParameters
    {
        /// <summary>
        ///     需要更新的区块数组
        /// </summary>
        public TerrainChunk[] Chunks;

        /// <summary>
        ///     更新位置索引字典
        /// </summary>
        public Dictionary<int, TerrainUpdateLocation> Locations;

        public TerrainUpdateLocation[] EffectiveLocations;
    }

    /// <summary>
    ///     光源
    /// </summary>
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
