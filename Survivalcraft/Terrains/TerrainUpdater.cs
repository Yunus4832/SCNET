using Game.Network;
using Game.Network.Packages;
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
    ///     慢速地形更新间隔（帧数）
    /// </summary>
    public static int SlowTerrainUpdate;

    /// <summary>
    ///     是否记录地形更新统计信息
    /// </summary>
    public static bool LogTerrainUpdateStats;

    /// <summary>
    ///     区块更新计数
    /// </summary>
    public static int ChunkUpdates;

    /// <summary>
    ///     光源列表
    /// </summary>
    private readonly DynamicArray<LightSource> _lightSources = [];

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
    private readonly Dictionary<int, UpdateLocation?> _pendingLocations = new();

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

    /// <summary>
    ///     客户端请求的待同步区块列表（用于联机模式）
    /// </summary>
    private readonly List<ChunkContentRequest> _requestSyncChunkList = [];

    private readonly List<ClientChunkSnapshot> _receivedChunkSnapshots = [];

    private readonly List<ChunkAllocationId> _failedChunkAllocations = [];

    private readonly List<TerrainCellDelta> _receivedCellDeltas = [];

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
    ///     动画纹理子系统
    /// </summary>
    private readonly SubsystemAnimatedTextures _subsystemAnimatedTextures;

    /// <summary>
    ///     游戏信息子系统
    /// </summary>
    private readonly SubsystemGameInfo _subsystemGameInfo;

    /// <summary>
    ///     季节子系统
    /// </summary>
    private readonly SubsystemSeasons _subsystemSeasons;

    /// <summary>
    ///     天空子系统
    /// </summary>
    private readonly SubsystemSky _subsystemSky;

    /// <summary>
    ///     请求同步更新的帧索引
    /// </summary>
    private int _synchronousUpdateFrame;

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

    private double _lastNetworkChunkDiagnosticTime;

    /// <summary>
    ///     更新位置字典
    /// </summary>
    public Dictionary<int, UpdateLocation> UpdateLocations => _updateParameters.Locations;

    /// <summary>
    ///     区块初始化完成事件
    /// </summary>
    public event Action<TerrainChunk>? OnChunkInit;

    /// <summary>
    ///     区块被释放事件
    /// </summary>
    public event Action<TerrainChunk>? OnChunkDiscard;

#pragma warning disable CS0067 // Event is never used
    /// <summary>
    ///     区块初始化事件（已弃用）
    /// </summary>
    public event Action<TerrainChunk>? ChunkInitialized;
#pragma warning restore CS0067 // Event is never used

    /// <summary>
    ///     地形更新进度完成事件
    /// </summary>
    public event Action? UpdateProgressFinished;

    /// <summary>
    ///     初始化 TerrainUpdater 实例
    /// </summary>
    /// <param name="subsystemTerrain">地形子系统</param>
    public TerrainUpdater(SubsystemTerrain subsystemTerrain)
    {
        _subsystemTerrain = subsystemTerrain;
        _subsystemGameInfo = _subsystemTerrain.Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemSeasons = _subsystemTerrain.Project.FindSubsystem<SubsystemSeasons>(true)!;
        _subsystemSky = _subsystemTerrain.Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemBlockBehaviors = _subsystemTerrain.Project.FindSubsystem<SubsystemBlockBehaviors>(true)!;
        _subsystemAnimatedTextures = _subsystemTerrain.Project.FindSubsystem<SubsystemAnimatedTextures>(true)!;
        _terrain = subsystemTerrain.Terrain;
        _updateParameters.Chunks = [];
        _updateParameters.Locations = new Dictionary<int, UpdateLocation>();
        _threadUpdateParameters.Chunks = [];
        _threadUpdateParameters.Locations = new Dictionary<int, UpdateLocation>();
        if (_subsystemTerrain.ContentRole == TerrainContentRole.Authority)
        {
            ServerChunkDistribution = new ServerChunkDistributionScheduler(
                _subsystemTerrain.ChunkContentAuthority ??
                throw new InvalidOperationException("Authoritative terrain requires a content authority."),
                Math.Max(1, SettingsManager.Current.ServerChunkCountSendPer));
        }

        SettingsManager.BrightnessChanged += SettingsManagerBrightnessChanged;
        SetUpdateLocation(-1, Vector2.Zero, 4, 4);
        _task = Task.Factory.StartNew(
            ThreadUpdateFunction,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        UnpauseUpdateThread();
        UpdateEvent.Set();
    }

    /// <summary>
    ///     释放资源
    /// </summary>
    public void Dispose()
    {
        SettingsManager.BrightnessChanged -= SettingsManagerBrightnessChanged;
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
        _synchronousUpdateFrame = Time.FrameIndex;
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
    ///     在 <see cref="_updateParameters" /> 中查找指定索引的 Location，
    ///     如果参数发生变化或位置移动超过阈值，则更新并加入待处理列表
    /// </remarks>
    /// <param name="locationIndex">位置索引</param>
    /// <param name="center">位置中心坐标</param>
    /// <param name="visibilityDistance">可视距离</param>
    /// <param name="contentDistance">内容加载距离</param>
    public void SetUpdateLocation(int locationIndex, Vector2 center, float visibilityDistance, float contentDistance)
    {
        contentDistance = MathUtils.Max(contentDistance, visibilityDistance);
        _updateParameters.Locations.TryGetValue(locationIndex, out var location);
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
        if (_subsystemTerrain.UsesRemoteChunkTransport)
        {
            CommonLib.Net.QueuePackage(new PlayerDataPackage(location));
        }
    }

    /// <summary>
    ///     移除指定索引的更新位置
    /// </summary>
    /// <param name="locationIndex">位置索引</param>
    public void RemoveUpdateLocation(int locationIndex)
    {
        _updateParameters.Locations.Remove(locationIndex);
    }


    /// <summary>
    ///     获取指定位置的区块更新进度
    /// </summary>
    /// <param name="locationIndex">位置索引</param>
    /// <param name="visibilityDistance">可视距离</param>
    /// <param name="contentDistance">内容加载距离</param>
    /// <returns>更新进度，范围 0.0~1.0</returns>
    public float GetUpdateProgress(int locationIndex, float visibilityDistance, float contentDistance)
    {
        var validChunkCount = 0;
        var invalidChunkCount = 0;
        if (!_updateParameters.Locations.TryGetValue(locationIndex, out var location))
        {
            return 0f;
        }

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
                    v1: location.Center);
                if (distanceSqrFromChunkToLocation <= visibilityDistanceSqr)
                {
                    if (chunk == null || chunk.MainThreadState < TerrainChunkState.Valid)
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

        // 如果温湿度变化，降级所有区块到 InvalidVertices1 状态
        if (Time.PeriodicEvent(1, 0.0))
        {
            var temperature =
                (int)MathUtils.Round(_temperatureCurve.Sample(_subsystemGameInfo.WorldSettings.TimeOfYear));
            var humidity = (int)MathUtils.Round(_humidityCurve.Sample(_subsystemGameInfo.WorldSettings.TimeOfYear));
            if (temperature != _terrain.SeasonTemperature || humidity != _terrain.SeasonHumidity)
            {
                _terrain.SeasonTemperature = temperature;
                _terrain.SeasonHumidity = humidity;
                DowngradeAllChunksState(TerrainChunkState.InvalidVertices1, false);
            }
        }

        // 是否有需要处理的 pendingLocations
        if (_pendingLocations.Count > 0)
        {
            // 暂停地形更新线程
            _pauseEvent.Reset();
            // 不阻塞主线程；后台步骤完成后在后续帧继续处理位置变更。
            if (UpdateEvent.WaitOne(0))
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
                    var allocationResult = AllocateAndFreeChunks(_updateParameters.Locations.Values.ToArray());
                    if (allocationResult.Changed)
                    {
                        _updateParameters.Chunks = _terrain.AllocatedChunks;
                    }

                    // 存档队列满时保留位置变更，后续帧继续卸载旧区块并分配新区块。
                    if (CanCompleteLocationUpdate(allocationResult.RetryRequired))
                    {
                        _pendingLocations.Clear();
                    }
                }
                finally
                {
                    // 重新设置 AutoResetEvent 令牌，让阻塞的更新线程恢复执行
                    UpdateEvent.Set();
                }
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
            if (_subsystemTerrain.UsesRemoteChunkTransport)
            {
                var now = Time.RealTime;
                if (terrainChunk.WorkerState == TerrainChunkState.NotLoaded &&
                    IsChunkInRange(
                        terrainChunk.Center,
                        activeLocations) &&
                    ShouldRequestNetworkChunk(terrainChunk, now))
                {
                    terrainChunk.IsRequested = true;
                    terrainChunk.NetworkRequestTime = now;
                    terrainChunk.NetworkRequestAttempts++;

                    lock (_requestSyncChunkList)
                    {
                        _requestSyncChunkList.Add(new ChunkContentRequest(
                            new ChunkAllocationId(
                                terrainChunk.Coords,
                                terrainChunk.AllocationGeneration),
                            terrainChunk.NetworkContentVersion));
                    }
                }
            }

            if (terrainChunk.MainThreadState < TerrainChunkState.InvalidVertices1 || terrainChunk.AreBehaviorsNotified)
            {
                continue;
            }

            terrainChunk.AreBehaviorsNotified = true;
            NotifyBlockBehaviors(terrainChunk);
        }

        LogStalledNetworkChunks();

        lock (_requestSyncChunkList)
        {
            if (_requestSyncChunkList.Count <= 0 || !Time.PeriodicEvent(0.5, 0.0))
            {
                return;
            }

            var transport = _subsystemTerrain.ChunkContentTransport ??
                            throw new InvalidOperationException(
                                "Client terrain requests require a chunk content transport.");
            transport.Request(_requestSyncChunkList);
            _requestSyncChunkList.Clear();
        }
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
            _receivedChunkSnapshots.Clear();
            transport.DrainReceived(_receivedChunkSnapshots);
            foreach (var snapshot in _receivedChunkSnapshots)
            {
                coordinator.TryInstall(snapshot);
            }

            _receivedCellDeltas.Clear();
            transport.DrainDeltas(_receivedCellDeltas);
            var deltaCoordinator = _subsystemTerrain.ClientTerrainDeltas;
            if (deltaCoordinator != null)
            {
                foreach (var delta in _receivedCellDeltas)
                {
                    deltaCoordinator.Receive(delta);
                }
            }

            _failedChunkAllocations.Clear();
            transport.DrainFailed(_failedChunkAllocations);
            foreach (var allocation in _failedChunkAllocations)
            {
                var chunk = _terrain.GetChunkAtCoords(allocation.Coords.X, allocation.Coords.Y);
                if (chunk == null || chunk.AllocationGeneration != allocation.Generation)
                {
                    continue;
                }

                chunk.IsRequested = false;
                chunk.NetworkRequestTime = 0.0;
                TerrainChunkStateExchange.RequestDowngrade(chunk, chunk.MainThreadState);
            }

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
               chunk.NetworkRequestTime <= 0.0 ||
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
            .Where(chunk => IsNetworkChunkStalled(chunk, now))
            .ToArray();
        if (stalled.Length == 0)
        {
            return;
        }

        Log.Warning($"Terrain chunk stalled summary: count={stalled.Length}.");
    }

    internal static bool IsNetworkChunkStalled(TerrainChunk chunk, double now)
    {
        if (!chunk.IsLoaded)
        {
            return chunk.IsRequested && now - chunk.NetworkRequestTime >= NetworkChunkRequestRetrySeconds;
        }

        if (chunk.NetworkContentReceiveTime <= 0.0 ||
            now - chunk.NetworkContentReceiveTime < NetworkChunkRequestRetrySeconds)
        {
            return false;
        }

        // Lighting or neighbor invalidation may rebuild derived data while the currently
        // uploaded geometry for the same authoritative contents remains visible.
        if (ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk))
        {
            return false;
        }

        return chunk.WorkerState < TerrainChunkState.Valid ||
               chunk.MainThreadState < TerrainChunkState.Valid ||
               !chunk.GeometryUploaded;
    }

    /// <summary>
    ///     准备绘制前的同步更新
    /// </summary>
    /// <param name="camera">当前摄像机</param>
    /// <remarks>
    ///     设置相机位置为更新位置，并立即同步更新视野内的关键区块
    /// </remarks>
    public void PrepareForDrawing(Camera camera)
    {
        SetUpdateLocation(camera.GameWidget.PlayerData.PlayerIndex, camera.ViewPosition.XZ,
            SettingsManager.Current.VisibilityRange, 64f);
        if (_synchronousUpdateFrame != Time.FrameIndex)
        {
            return;
        }

        var list = DetermineSynchronousUpdateChunks(camera.ViewPosition, camera.ViewDirection);
        if (list.Count <= 0)
        {
            return;
        }

        UpdateEvent.WaitOne();
        try
        {
            SendReceiveChunkStates();
            SendReceiveChunkStatesThread();
            foreach (var item in list)
            {
                if (!CanSynchronouslyUpdateChunk(_subsystemTerrain.ContentRole, item))
                {
                    continue;
                }

                while (item.WorkerState < TerrainChunkState.Valid)
                {
                    var dependency = ClientDerivedTerrainPolicy.FindPendingLightingDependency(
                        _terrain,
                        _subsystemTerrain.ContentRole,
                        item);
                    UpdateChunkSingleStep(dependency ?? item, _subsystemSky.SkyLightValue);
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
    /// <param name="forceGeometryRegeneration">是否强制重新生成几何体</param>
    public void DowngradeChunkNeighborhoodState(Point2 coordinates, int radius, TerrainChunkState state,
        bool forceGeometryRegeneration)
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
                    chunkAtCoords.MainThreadState = state;
                    if (forceGeometryRegeneration)
                    {
                        chunkAtCoords.InvalidateSliceContentsHashes();
                    }
                }

                TerrainChunkStateExchange.RequestDowngrade(chunkAtCoords, state);
            }
        }
    }

    /// <summary>
    ///     降级所有已分配区块的状态
    /// </summary>
    /// <param name="state">目标降级状态</param>
    /// <param name="forceGeometryRegeneration">是否强制重新生成几何体</param>
    public void DowngradeAllChunksState(TerrainChunkState state, bool forceGeometryRegeneration)
    {
        var allocatedChunks = _terrain.AllocatedChunks;
        foreach (var terrainChunk in allocatedChunks)
        {
            if (terrainChunk.MainThreadState > state)
            {
                terrainChunk.MainThreadState = state;
                if (forceGeometryRegeneration)
                {
                    terrainChunk.InvalidateSliceContentsHashes();
                }
            }

            TerrainChunkStateExchange.RequestDowngrade(terrainChunk, state);
        }
    }

    /// <summary>
    ///     检查区块中心是否在任一更新位置的范围内
    /// </summary>
    /// <param name="chunkCenter">区块中心坐标</param>
    /// <param name="locations">更新位置数组</param>
    /// <returns>如果在任一位置的 ContentDistance 范围内则返回 true</returns>
    internal static bool IsChunkInRange(
        Vector2 chunkCenter,
        UpdateLocation[] locations,
        float distanceMargin = 0f)
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
    private ChunkAllocationResult AllocateAndFreeChunks(UpdateLocation[] locations)
    {
        var result = false;
        var hasDeferredChunkDiscards = false;
        var allocatedChunks = _terrain.AllocatedChunks;
        HashSet<Point2> retentionEvictions = [];
        if (_subsystemTerrain.UsesRemoteChunkTransport)
        {
            var access = ++_remoteChunkAccessClock;
            foreach (var chunk in allocatedChunks.Where(chunk =>
                         IsChunkInRange(chunk.Center, locations)))
            {
                _remoteChunkLastAccess[chunk.Coords] = access;
            }

            var retained = allocatedChunks
                .Where(chunk => !IsChunkInRange(chunk.Center, locations))
                .Where(chunk => IsChunkInRange(
                    chunk.Center,
                    locations,
                    NetworkTerrainPolicy.ClientChunkRetentionMargin))
                .Select(chunk => (chunk.Coords, _remoteChunkLastAccess.GetValueOrDefault(chunk.Coords)))
                .ToArray();
            retentionEvictions = SelectRetainedChunkCoordsToEvict(
                    retained,
                    NetworkTerrainPolicy.ClientRetainedChunkCapacity)
                .ToHashSet();
        }

        foreach (var terrainChunk in allocatedChunks)
        {
            var retentionMargin = _subsystemTerrain.UsesRemoteChunkTransport
                ? NetworkTerrainPolicy.ClientChunkRetentionMargin
                : 0f;
            if (IsChunkInRange(terrainChunk.Center, locations, retentionMargin) &&
                !retentionEvictions.Contains(terrainChunk.Coords))
            {
                continue;
            }

            var saveCoordinator = _subsystemTerrain.ContentRole == TerrainContentRole.Authority
                ? _subsystemTerrain.TerrainSaveCoordinator
                : null;
            if (saveCoordinator != null && TerrainSaveCoordinator.RequiresSave(terrainChunk) &&
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

            CurrentModRuntime.Value?.Gameplay.Invoke(new TerrainChunkDiscardingContext(
                _subsystemTerrain,
                terrainChunk));

            var localHost = _subsystemTerrain.LocalChunkHost;
            var localAuthorityChunk = localHost?.AuthorityTerrain.GetChunkAtCoords(
                terrainChunk.Coords.X,
                terrainChunk.Coords.Y);
            if (localAuthorityChunk != null && !localHost!.Release(terrainChunk.Coords))
            {
                hasDeferredChunkDiscards = true;
                continue;
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
                _subsystemTerrain.TerrainSerializer.SaveChunk(terrainChunk);
            }

            result = true;
            _subsystemTerrain.ClientTerrainDeltas?.Discard(terrainChunk.Coords);
            _terrain.FreeChunk(terrainChunk);
            _remoteChunkLastAccess.Remove(terrainChunk.Coords);
            if (RunMode.Value is RunModeType.Gui)
            {
                _subsystemTerrain.TerrainRenderer.DisposeTerrainChunkGeometryVertexIndexBuffers(terrainChunk);
            }
        }

        if (hasDeferredChunkDiscards)
        {
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
                        if (_subsystemTerrain.UsesRemoteChunkTransport)
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
        int capacity)
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
                Log.Error(ExceptionManager.MakeFullErrorMessage("Terrain update worker failed.", e));
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

        // 查找最佳的更新区块
        var terrainChunk = FindBestChunkToUpdate(out var desiredState);
        if (terrainChunk == null)
        {
            return true;
        }

        // 如果区块不是预期的状态，则更新它
        if (terrainChunk.WorkerState < desiredState)
        {
            UpdateChunkSingleStep(terrainChunk, _subsystemSky.SkyLightValue);
        }

        return false;
    }

    /// <summary>
    ///     查找最适合更新的区块（按距离优先级）
    /// </summary>
    /// <remarks>
    ///     使用距离收敛策略：从最近的区块开始，优先更新可视范围内的区块到 Valid 状态，
    ///     其次是内容范围内的区块到 InvalidVertices1 状态
    /// </remarks>
    /// <param name="desiredState">输出参数，找到区块的目标状态</param>
    /// <returns>需要更新的区块，如果没有则返回 null</returns>
    private TerrainChunk? FindBestChunkToUpdate(out TerrainChunkState desiredState)
    {
        var chunks = _threadUpdateParameters.Chunks;
        var locations = _threadUpdateParameters.Locations.Values;

        // 初始的距离（平方，下面都用距离替代）的最大限制
        var maxDistanceSquared = 3.40282347E+38f;

        TerrainChunk? result = null;
        desiredState = TerrainChunkState.NotLoaded;

        // 逐渐收敛距离的最大限制，找到最合适的更新区块和预期的状态
        foreach (var terrainChunk in chunks)
        {
            // A client cannot advance a chunk until its contents arrive from the server.
            // Selecting a nearby NotLoaded chunk repeatedly would otherwise starve loaded
            // chunks (for example, chunks waiting for lighting and geometry) under packet loss.
            if (!CanBackgroundUpdateChunk(_subsystemTerrain.ContentRole, terrainChunk))
            {
                continue;
            }

            // 跳过 Valid 状态的区块
            if (terrainChunk.WorkerState >= TerrainChunkState.Valid)
            {
                continue;
            }

            foreach (var location in locations)
            {
                // 位置与区块中心的距离
                var distanceSquared = Vector2.DistanceSquared(location.Center, terrainChunk.Center);
                // 距离大于最大限制的区块，跳过
                if (!(distanceSquared < maxDistanceSquared))
                {
                    continue;
                }

                // 距离小于位置 location 的可视范围，区块的预期状态设置为 Valid 将最大限制设置为区块与位置距离
                if (distanceSquared <= MathUtils.Sqr(location.VisibilityDistance))
                {
                    desiredState = TerrainChunkState.Valid;
                    maxDistanceSquared = distanceSquared;
                    result = terrainChunk;
                }
                // 否则，如果区块的线程状态小于 InvalidVertices1, 并且距离小于位置的 ContentDistance 的距离，
                // 则预期的区块状态为 InvalidVertices1, 更新最大限制为当前距离
                else if (terrainChunk.WorkerState < TerrainChunkState.InvalidVertices1 &&
                         distanceSquared <= MathUtils.Sqr(location.ContentDistance))
                {
                    desiredState = TerrainChunkState.InvalidVertices1;
                    maxDistanceSquared = distanceSquared;
                    result = terrainChunk;
                }
            }
        }

        if (result == null)
        {
            return result;
        }

        var dependency = ClientDerivedTerrainPolicy.FindPendingLightingDependency(
            _terrain,
            _subsystemTerrain.ContentRole,
            result
        );

        if (dependency == null)
        {
            return result;
        }

        desiredState = TerrainChunkState.InvalidPropagatedLight;
        return dependency;
    }

    internal static bool CanBackgroundUpdateChunk(TerrainContentRole role, TerrainChunk chunk) =>
        role != TerrainContentRole.Replica || chunk.WorkerState != TerrainChunkState.NotLoaded;

    public void NotifyNetworkChunkLoaded()
    {
        UnpauseUpdateThread();
    }

    public void OnNetworkChunkContentInstalled(TerrainChunk target)
    {
        var derivation = _subsystemTerrain.ClientChunkDerivation ??
                         throw new InvalidOperationException(
                             "Installed client contents require a derivation pipeline.");
        derivation.Begin(target);
        target.IsRequested = false;
        target.NetworkRequestTime = 0.0;
        target.NetworkContentReceiveTime = Time.RealTime;
        target.NetworkGeometryReadyTime = 0.0;
        target.NetworkGeometryUploadTime = 0.0;
        target.GeometryUploaded = false;
        NotifyNetworkChunkLoaded();
    }

    /// <summary>
    ///     确定需要同步更新的关键区块（视野内优先）
    /// </summary>
    /// <param name="viewPosition">视点位置</param>
    /// <param name="viewDirection">视线方向</param>
    /// <returns>需要同步更新的区块列表</returns>
    private List<TerrainChunk> DetermineSynchronousUpdateChunks(Vector3 viewPosition, Vector3 viewDirection)
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
            var chunkAtCell = _terrain.GetChunkAtCell(Terrain.ToCell(vector2.X), Terrain.ToCell(vector2.Z), false);
            if (chunkAtCell is { MainThreadState: < TerrainChunkState.Valid } && !list.Contains(chunkAtCell))
            {
                list.Add(chunkAtCell);
            }
        }

        return list;
    }

    /// <summary>
    ///     单步更新区块的状态
    /// </summary>
    /// <param name="chunk">要更新的区块</param>
    /// <param name="skylightValue">当前天空光照值</param>
    /// <remarks>
    ///     根据区块当前状态执行相应的处理：加载数据、生成内容、计算光照、生成顶点等
    /// </remarks>
    private void UpdateChunkSingleStep(TerrainChunk chunk, int skylightValue)
    {
        if (chunk.WorkerState <= TerrainChunkState.InvalidContents4)
        {
            var generation = _subsystemTerrain.AuthoritativeChunkGeneration;
            if (generation == null)
            {
                throw new InvalidOperationException(
                    $"Client attempted authoritative terrain generation at {chunk.Coords} ({chunk.WorkerState}).");
            }

            generation.TryAdvance(chunk);
            return;
        }

        switch (chunk.WorkerState)
        {
            case TerrainChunkState.InvalidLight:
                {
                    GenerateChunkSunLightAndHeight(chunk, skylightValue);
                    chunk.WorkerState = TerrainChunkState.InvalidPropagatedLight;
                    chunk.LightPropagationMask = 0;
                    break;
                }
            case TerrainChunkState.InvalidPropagatedLight:
                {
                    _lightSources.Clear();
                    for (var k = -1; k <= 1; k++)
                    {
                        for (var l = -1; l <= 1; l++)
                        {
                            var num = CalculateLightPropagationBitIndex(k, l);
                            if (((chunk.LightPropagationMask >> num) & 1) != 0)
                            {
                                continue;
                            }

                            var chunkAtCell2 = _terrain.GetChunkAtCell(chunk.Origin.X + k * 16, chunk.Origin.Y + l * 16, false);
                            if (chunkAtCell2 == null ||
                                !ClientDerivedTerrainPolicy.CanAdvanceLightingDependency(
                                    _subsystemTerrain.ContentRole,
                                    chunkAtCell2))
                            {
                                continue;
                            }

                            GenerateChunkLightSources(chunkAtCell2);
                            UpdateNeighborsLightPropagationBitmasks(chunkAtCell2);
                        }
                    }

                    PropagateLight();
                    chunk.WorkerState = TerrainChunkState.InvalidVertices1;
                    break;
                }
            case TerrainChunkState.InvalidVertices1:
                {
                    if (RunMode.Value is RunModeType.HeadlessServer)
                    {
                        chunk.WorkerState = TerrainChunkState.Valid;
                        break;
                    }

                    CalculateChunkSliceContentsHash(chunk);
                    lock (chunk.Geometry)
                    {
                        chunk.NewGeometryData = false;
                        GenerateChunkVertices(chunk, true);
                    }

                    chunk.WorkerState = TerrainChunkState.InvalidVertices2;
                    break;
                }
            case TerrainChunkState.InvalidVertices2:
                {
                    if (RunMode.Value is RunModeType.HeadlessServer)
                    {
                        chunk.WorkerState = TerrainChunkState.Valid;
                        break;
                    }

                    lock (chunk.Geometry)
                    {
                        GenerateChunkVertices(chunk, false);
                        if (_subsystemTerrain.ContentRole == TerrainContentRole.Replica &&
                            chunk.NetworkContentReceiveTime > 0.0)
                        {
                            ClientChunkDerivationPipeline.CompleteGeometry(chunk);
                            chunk.NetworkGeometryReadyTime = Time.RealTime;
                        }

                        // Publish only after the geometry version and timing metadata describe
                        // the buffers that the renderer is about to upload.
                        chunk.NewGeometryData = true;
                    }

                    chunk.WorkerState = TerrainChunkState.Valid;
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
    private void GenerateChunkSunLightAndHeight(TerrainChunk chunk, int skylightValue)
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
                    var cellValueFast = chunk.GetCellValueFast(num5);
                    if (Terrain.ExtractContents(cellValueFast) != 0)
                    {
                        num = num4;
                        break;
                    }

                    cellValueFast = Terrain.ReplaceLight(cellValueFast, skylightValue);
                    chunk.SetCellValueFast(num5, cellValueFast);
                    num4--;
                    num5--;
                }

                num4 = 0;
                num5 = TerrainChunk.CalculateCellIndex(i, 0, j);
                while (num4 <= num + 1)
                {
                    var cellValueFast2 = chunk.GetCellValueFast(num5);
                    var num6 = Terrain.ExtractContents(cellValueFast2);
                    if (BlocksManager.Blocks[num6].Transparent)
                    {
                        num2 = num4;
                        break;
                    }

                    cellValueFast2 = Terrain.ReplaceLight(cellValueFast2, 0);
                    chunk.SetCellValueFast(num5, cellValueFast2);
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
                        var cellValueFast3 = chunk.GetCellValueFast(num5);
                        var num8 = Terrain.ExtractContents(cellValueFast3);
                        if (num8 != 0)
                        {
                            var block = BlocksManager.Blocks[num8];
                            if (!block.Transparent || block.LightAttenuation >= num7)
                            {
                                break;
                            }

                            num7 -= block.LightAttenuation;
                        }

                        cellValueFast3 = Terrain.ReplaceLight(cellValueFast3, num7);
                        chunk.SetCellValueFast(num5, cellValueFast3);
                        num4--;
                        num5--;
                    }
                }

                var num3 = num4 + 1;
                while (num4 >= num2)
                {
                    var cellValueFast4 = chunk.GetCellValueFast(num5);
                    cellValueFast4 = Terrain.ReplaceLight(cellValueFast4, 0);
                    chunk.SetCellValueFast(num5, cellValueFast4);
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
        var blocks = BlocksManager.Blocks;
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
                    var cellValueFast = chunk.GetCellValueFast(num5);
                    var num6 = 0;
                    var block = blocks[Terrain.ExtractContents(cellValueFast)];
                    if (num4 >= num3 && block.Transparent)
                    {
                        var cellLightFast = chunkAtCell.GetCellLightFast(x, num4, z);
                        var cellLightFast2 = chunkAtCell2.GetCellLightFast(x2, num4, z2);
                        var cellLightFast3 = chunkAtCell3.GetCellLightFast(x3, num4, z3);
                        var cellLightFast4 = chunkAtCell4.GetCellLightFast(x4, num4, z4);
                        num6 = MathUtils.Max(cellLightFast, cellLightFast2, cellLightFast3, cellLightFast4) - 1 -
                               block.LightAttenuation;
                    }

                    if (block.EmittedLightAmount > 0)
                    {
                        num6 = MathUtils.Max(num6, block.GetEmittedLightAmount(cellValueFast));
                    }

                    if (num6 > Terrain.ExtractLight(cellValueFast))
                    {
                        chunk.SetCellValueFast(num5, Terrain.ReplaceLight(cellValueFast, num6));
                        _lightSources.Add(new LightSource
                        {
                            X = num,
                            Y = num4,
                            Z = num2,
                            Light = num6
                        });
                    }

                    num4++;
                    num5++;
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
        var cellValueFast = chunkAtCell.GetCellValueFast(index);
        var num = Terrain.ExtractContents(cellValueFast);
        var block = BlocksManager.Blocks[num];
        if (!block.Transparent)
        {
            return;
        }

        var num2 = light - block.LightAttenuation - 1;
        if (num2 <= Terrain.ExtractLight(cellValueFast))
        {
            return;
        }

        _lightSources.Add(new LightSource
        {
            X = x,
            Y = y,
            Z = z,
            Light = num2
        });
        chunkAtCell.SetCellValueFast(index, Terrain.ReplaceLight(cellValueFast, num2));
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
    ///     生成区块的顶点数据
    /// </summary>
    /// <param name="chunk">目标区块</param>
    /// <param name="even">是否生成偶数层的切片</param>
    /// <remarks>
    ///     分两遍生成：第一遍生成偶数切片，第二遍生成奇数切片
    /// </remarks>
    private void GenerateChunkVertices(TerrainChunk chunk, bool even)
    {
        _subsystemTerrain.BlockGeometryGenerator.ResetCache();
        if (!chunk.Draws.TryGetValue(_subsystemAnimatedTextures.AnimatedBlocksTexture, out var terrainGeometry))
        {
            terrainGeometry = new TerrainGeometry[TerrainChunk.SlicesCount];
            for (var i = 0; i < TerrainChunk.SlicesCount; i++)
            {
                var t = new TerrainGeometry(chunk.Draws, i);
                terrainGeometry[i] = t;
            }

            chunk.Draws.Add(_subsystemAnimatedTextures.AnimatedBlocksTexture, terrainGeometry);
        }

        var chunkAtCoords = _terrain.GetChunkAtCoords(chunk.Coords.X - 1, chunk.Coords.Y - 1);
        var chunkAtCoords2 = _terrain.GetChunkAtCoords(chunk.Coords.X, chunk.Coords.Y - 1);
        var chunkAtCoords3 = _terrain.GetChunkAtCoords(chunk.Coords.X + 1, chunk.Coords.Y - 1);
        var chunkAtCoords4 = _terrain.GetChunkAtCoords(chunk.Coords.X - 1, chunk.Coords.Y);
        var chunkAtCoords5 = _terrain.GetChunkAtCoords(chunk.Coords.X + 1, chunk.Coords.Y);
        var chunkAtCoords6 = _terrain.GetChunkAtCoords(chunk.Coords.X - 1, chunk.Coords.Y + 1);
        var chunkAtCoords7 = _terrain.GetChunkAtCoords(chunk.Coords.X, chunk.Coords.Y + 1);
        var chunkAtCoords8 = _terrain.GetChunkAtCoords(chunk.Coords.X + 1, chunk.Coords.Y + 1);
        var num = 0;
        var num2 = 0;
        var num3 = 16;
        var num4 = 16;
        if (chunkAtCoords4 == null)
        {
            num++;
        }

        if (chunkAtCoords2 == null)
        {
            num2++;
        }

        if (chunkAtCoords5 == null)
        {
            num3--;
        }

        if (chunkAtCoords7 == null)
        {
            num4--;
        }

        for (var i = 0; i < TerrainChunk.SlicesCount; i++)
        {
            if (i >= 17)
            {
            }

            if (i % 2 == 0 != even)
            {
                continue;
            }

            chunk.SliceContentsHashes[i] = CalculateChunkSliceContentsHash(chunk, i);
            var generateHash = chunk.GeneratedSliceContentsHashes[i];
            if (generateHash != 0 && generateHash == chunk.SliceContentsHashes[i])
            {
                continue;
            }

            foreach (var c in chunk.Draws)
            {
                var subsets = c.Value[i].Subsets;
                foreach (var subset in subsets)
                {
                    subset.Vertices.Clear();
                    subset.Indices.Clear();
                }
            }

            for (var k = num; k < num3; k++)
            {
                for (var l = num2; l < num4; l++)
                {
                    switch (k)
                    {
                        case 0:
                            if ((l == 0 && chunkAtCoords == null) || (l == 15 && chunkAtCoords6 == null))
                            {
                                continue;
                            }

                            break;
                        case 15:
                            if ((l == 0 && chunkAtCoords3 == null) || (l == 15 && chunkAtCoords8 == null))
                            {
                                continue;
                            }

                            break;
                    }

                    var num5 = k + chunk.Origin.X;
                    var num6 = l + chunk.Origin.Y;
                    var bottomHeightFast = chunk.GetBottomHeightFast(k, l);
                    var bottomHeight = _terrain.GetBottomHeight(num5 - 1, num6);
                    var bottomHeight2 = _terrain.GetBottomHeight(num5 + 1, num6);
                    var bottomHeight3 = _terrain.GetBottomHeight(num5, num6 - 1);
                    var bottomHeight4 = _terrain.GetBottomHeight(num5, num6 + 1);
                    var x = MathUtils.Min(bottomHeightFast - 1,
                        MathUtils.Min(bottomHeight, bottomHeight2, bottomHeight3, bottomHeight4));
                    var x2 = chunk.GetTopHeightFast(k, l) + 1;
                    var num7 = MathUtils.Max(16 * i, x, 1);
                    var num8 = MathUtils.Min(16 * (i + 1), x2, 256);
                    var num9 = TerrainChunk.CalculateCellIndex(k, 0, l);
                    for (var m = num7; m < num8; m++)
                    {
                        var cellValueFast = chunk.GetCellValueFast(num9 + m);
                        var num10 = Terrain.ExtractContents(cellValueFast);
                        if (num10 != 0)
                        {
                            BlocksManager.Blocks[num10].GenerateTerrainVertices(_subsystemTerrain.BlockGeometryGenerator,
                                terrainGeometry[i], cellValueFast, num5, m, num6);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    ///     计算光照传播位掩码的索引
    /// </summary>
    /// <param name="x">相对 X 偏移（-1, 0, 1）</param>
    /// <param name="z">相对 Z 偏移（-1, 0, 1）</param>
    /// <returns>位掩码索引（0-8）</returns>
    private static int CalculateLightPropagationBitIndex(int x, int z)
    {
        return x + 1 + 3 * (z + 1);
    }

    /// <summary>
    ///     更新邻近区块的光照传播位掩码
    /// </summary>
    /// <param name="chunk">当前区块</param>
    /// <remarks>
    ///     标记周围 3x3 区域的区块，表示它们需要重新计算光照
    /// </remarks>
    private void UpdateNeighborsLightPropagationBitmasks(TerrainChunk chunk)
    {
        for (var i = -1; i <= 1; i++)
        {
            for (var j = -1; j <= 1; j++)
            {
                var chunkAtCoords = _terrain.GetChunkAtCoords(chunk.Coords.X + i, chunk.Coords.Y + j);
                if (chunkAtCoords == null)
                {
                    continue;
                }

                var num = CalculateLightPropagationBitIndex(-i, -j);
                chunkAtCoords.LightPropagationMask |= 1 << num;
            }
        }
    }

    /// <summary>
    ///     计算区块切片的哈希值
    /// </summary>
    /// <param name="chunk">目标区块</param>
    /// <param name="sliceIndex">切片索引</param>
    /// <returns>计算得到的哈希值</returns>
    private int CalculateChunkSliceContentsHash(TerrainChunk chunk, int sliceIndex)
    {
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
                    num += chunkAtCell.GetCellValueFast(num10++);
                    num *= 31;
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

    /// <summary>
    ///     计算所有区块切片的哈希值
    /// </summary>
    /// <param name="chunk">目标区块</param>
    private void CalculateChunkSliceContentsHash(TerrainChunk chunk)
    {
        var num = 1;
        num += _terrain.SeasonTemperature;
        num *= 31;
        num += _terrain.SeasonHumidity;
        num *= 31;
        for (var i = 0; i < TerrainChunk.SlicesCount; i++)
        {
            chunk.SliceContentsHashes[i] = num;
        }

        var num2 = chunk.Origin.X - 1;
        var num3 = chunk.Origin.X + 16 + 1;
        var num4 = chunk.Origin.Y - 1;
        var num5 = chunk.Origin.Y + 16 + 1;
        for (var j = num2; j < num3; j++)
        {
            for (var k = num4; k < num5; k++)
            {
                var chunkAtCell = _terrain.GetChunkAtCell(j, k, false);
                if (chunkAtCell == null)
                {
                    continue;
                }

                var num6 = j & 15;
                var num7 = k & 15;
                var shaftValueFast = chunkAtCell.GetShaftValueFast(num6, num7);
                var num8 = Terrain.ExtractTopHeight(shaftValueFast);
                var num9 = Terrain.ExtractBottomHeight(shaftValueFast);
                var num10 = num6 > 0
                    ? chunkAtCell.GetBottomHeightFast(num6 - 1, num7)
                    : _terrain.GetBottomHeight(j - 1, k);
                var num11 = num7 > 0
                    ? chunkAtCell.GetBottomHeightFast(num6, num7 - 1)
                    : _terrain.GetBottomHeight(j, k - 1);
                var num12 = num6 < 15
                    ? chunkAtCell.GetBottomHeightFast(num6 + 1, num7)
                    : _terrain.GetBottomHeight(j + 1, k);
                var num13 = num7 < 15
                    ? chunkAtCell.GetBottomHeightFast(num6, num7 + 1)
                    : _terrain.GetBottomHeight(j, k + 1);
                var num14 = MathUtils.Min(MathUtils.Min(num10, num11, num12, num13), num9 - 1);
                var num15 = num8 + 2;
                num14 = MathUtils.Max(num14, 0);
                num15 = MathUtils.Min(num15, 256);
                var num16 = MathUtils.Max((num14 - 1) / 16, 0);
                var num17 = MathUtils.Min((num15 + 1) / 16, TerrainChunk.SlicesCount - 1);
                var num18 = 1;
                num18 += Terrain.ExtractTemperature(shaftValueFast);
                num18 *= 31;
                num18 += Terrain.ExtractHumidity(shaftValueFast);
                num18 *= 31;
                for (var l = num16; l <= num17; l++)
                {
                    var num19 = num18;
                    var num20 = MathUtils.Max(l * 16 - 1, num14);
                    var num21 = MathUtils.Min(l * 16 + 16 + 1, num15);
                    var m = TerrainChunk.CalculateCellIndex(num6, num20, num7);
                    var num22 = m + num21 - num20;
                    while (m < num22)
                    {
                        num19 += chunkAtCell.GetCellValueFast(m++);
                        num19 *= 31;
                    }

                    num19 += num20;
                    num19 *= 31;
                    chunk.SliceContentsHashes[l] += num19;
                }
            }
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

        CurrentModRuntime.Value?.Gameplay.Invoke(new TerrainChunkInitializedContext(
            _subsystemTerrain,
            chunk));
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

    private void SettingsManagerBrightnessChanged()
    {
        DowngradeAllChunksState(TerrainChunkState.InvalidVertices1, true);
    }

    public struct UpdateLocation
    {
        /// <summary>
        ///     Location 位置中心
        /// </summary>
        public Vector2 Center;

        /// <summary>
        ///     上次更新的位置中心（用于检测位置变化）
        /// </summary>
        public Vector2? LastChunksUpdateCenter;

        /// <summary>
        ///     可视距离
        /// </summary>
        public float VisibilityDistance;

        /// <summary>
        ///     内容加载距离（一般大于等于可视距离）
        /// </summary>
        public float ContentDistance;
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
        public Dictionary<int, UpdateLocation> Locations;
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
