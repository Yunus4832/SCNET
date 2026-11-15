using EntitySystem.Core;

using Game.Diagnostics;

namespace Game.World;

/// <summary>Single authority owner for loading, commands, simulation, world services and retirement.</summary>
public sealed class AuthorityWorldExecutor : IAsyncDisposable
{
    private const double _simulationPeriod = 1d / 20;

    private const double _servicePeriod = 1d / 60;

    private const int _requestCapacity = 256;

    private readonly Lock _gate = new();

    private readonly Queue<IRequest> _requests = new();

    private readonly AutoResetEvent _wake = new(false);

    private readonly CancellationTokenSource _loading = new();

    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Thread _thread;

    private readonly Func<CancellationToken, WorldRuntimeContext> _initialize;

    private readonly bool _allowsLocalPause;

    private WorldRuntimeContext? _world;

    private WorldProjectPersistence? _persistence;

    private NetNode? _endpoint;

    private StopRequest? _stop;

    private Task<WorldSaveOutcome>? _finalSave;

    private bool _requestedPause;

    private bool _signalDisposed;

    private long _serviceCycles;

    private double _maximumTickMilliseconds;

    private AuthorityWorldStatus _status = new(
        Guid.Empty,
        AuthorityExecutionState.Loading,
        false,
        0,
        0,
        0,
        false,
        0,
        0,
        null,
        []
    );

    public AuthorityWorldExecutor(Func<CancellationToken, WorldRuntimeContext> initialize, bool allowsLocalPause)
    {
        _initialize = initialize ?? throw new ArgumentNullException(nameof(initialize));
        _allowsLocalPause = allowsLocalPause;
        _thread = new Thread(Run) { Name = "SCNET Authority", IsBackground = true };
        _thread.Start();
    }

    public ServerTickStatistics TickStatistics { get; } = new();

    public Task Ready => _ready.Task;

    public Task Completion => _completion.Task;

    public AuthorityWorldStatus Status => Volatile.Read(ref _status);

    public bool IsOwnerThread => Environment.CurrentManagedThreadId == _thread.ManagedThreadId;

    /// <summary>The synchronous operation runs on the owner; only immutable results should leave it.</summary>
    public Task<T> InvokeAsync<T>(Func<WorldRuntimeContext, T> operation, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (cancellation.IsCancellationRequested) { return Task.FromCanceled<T>(cancellation); }

        if (IsOwnerThread && _world is { IsClosed: false } world)
        {
            try { return Task.FromResult(operation(world)); }
            catch (Exception error) { return Task.FromException<T>(error); }
        }

        lock (_gate)
        {
            if (_signalDisposed || Status.State != AuthorityExecutionState.Running || _stop != null)
            {
                return Task.FromException<T>(
                    new InvalidOperationException("The authority world is not accepting requests.")
                );
            }

            if (_requests.Count >= _requestCapacity)
            {
                return Task.FromException<T>(new InvalidOperationException("The authority request queue is full."));
            }

            var request = new Request<T>(operation, cancellation);
            _requests.Enqueue(request);
            _wake.Set();
            return request.Task.WaitAsync(cancellation);
        }
    }

    public void SetLocalPause(bool paused)
    {
        lock (_gate)
        {
            if (_signalDisposed) { return; }

            _requestedPause = paused;
            _wake.Set();
        }
    }

    /// <summary>Save failure keeps the owner alive and allows a later stop request to retry.</summary>
    public Task StopAsync(bool save)
    {
        lock (_gate)
        {
            if (_signalDisposed) { return Completion; }

            if (_stop != null) { return _stop.Completion.Task; }

            _stop = new StopRequest(save);
            if (!_ready.Task.IsCompleted) { _loading.Cancel(); }

            _wake.Set();
            return _stop.Completion.Task;
        }
    }

    private void Run()
    {
        Exception? failure = null;
        StopRequest? successfulStop = null;
        try
        {
            _world = _initialize(_loading.Token);
            if (!_world.IsAuthority || _world.HasPresentation || _world.Project == null)
            {
                throw new InvalidOperationException(
                    "Authority execution requires a loaded non-presentation authority world."
                );
            }

            _endpoint = WorldNetworkBinding.FindNode(_world);
            _world.BindExecutionThread();
            WorldProjectPersistence.TryGet(_world.Project, out _persistence);
            _world.Project.FindSubsystem<SubsystemWorldLifecycle>()?.BindStopRequest(() => StopAsync(true));
            if (_loading.IsCancellationRequested) { _ready.TrySetCanceled(_loading.Token); }
            else
            {
                Publish(AuthorityExecutionState.Running, false);
                _ready.TrySetResult();
            }

            var now = Time.RealTime;
            var lastSimulation = now - _simulationPeriod;
            var nextSimulation = now;
            var nextService = now;
            var nextSave = now + 120;
            while (!_world.IsClosed && _world.Project is { IsDisposed: false } project)
            {
                StopRequest? stop;
                lock (_gate) { stop = _stop; }

                if (stop != null)
                {
                    Publish(AuthorityExecutionState.Stopping, false);
                    if (_finalSave == null)
                    {
                        DrainRequests(double.PositiveInfinity);
                        if (_world.IsClosed || project.IsDisposed)
                        {
                            successfulStop = stop;
                            break;
                        }

                        if (!stop.Save)
                        {
                            successfulStop = stop;
                            break;
                        }

                        var stopNode = WorldNetworkBinding.FindNode(_world);
                        stop.DrainStartedAt ??= Time.RealTime;
                        stopNode?.Update();
                        if (_world.IsClosed || project.IsDisposed)
                        {
                            successfulStop = stop;
                            break;
                        }

                        if (stopNode?.PendingPackageCount > 0 &&
                            (Time.RealTime - stop.DrainStartedAt.Value) * 1000 <
                            NetworkTransportLimits.DisconnectTimeoutMilliseconds)
                        {
                            _wake.WaitOne(16);
                            continue;
                        }

                        _finalSave = WorldProjectPersistence.For(project).SaveAsync(final: true);
                    }

                    WorldProjectPersistence.For(project).Pump();
                    if (!_finalSave.IsCompleted)
                    {
                        _wake.WaitOne(16);
                        continue;
                    }

                    var result = _finalSave.GetAwaiter().GetResult();
                    _finalSave = null;
                    if (result.Failure == null)
                    {
                        successfulStop = stop;
                        break;
                    }

                    lock (_gate) { _stop = null; }

                    Publish(AuthorityExecutionState.Running, false);
                    stop.Completion.TrySetException(new WorldSaveException(result.WorldName, result.Failure));
                    now = Time.RealTime;
                    lastSimulation = now;
                    nextSimulation = now + _simulationPeriod;
                    nextService = now;
                }

                DrainRequests(nextService);
                if (WorldProjectPersistence.TryGet(project, out var persistence)) { persistence!.Pump(); }

                if (_world.IsClosed || project.IsDisposed) { break; }

                now = Time.RealTime;
                if (now < nextService)
                {
                    _wake.WaitOne(Math.Max(1, (int)Math.Ceiling((nextService - now) * 1000)));
                    continue;
                }

                var node = WorldNetworkBinding.FindNode(_world);
                bool requestedPause;
                lock (_gate) { requestedPause = _requestedPause; }

                var paused = _allowsLocalPause && requestedPause && node?.IsTransportRunning != true;
                project.FindSubsystem<SubsystemTime>()?.SetPaused(paused);
                var simulationLateness = Math.Max(0, (now - nextSimulation) * 1000);
                var simulate = now >= nextSimulation;
                if (simulate)
                {
                    _world.Clock.Advance((float)(now - lastSimulation), now);
                    lastSimulation = now;
                    nextSimulation = now + _simulationPeriod;
                }

                var started = System.Diagnostics.Stopwatch.GetTimestamp();
                node?.Update();
                if (_world.IsClosed || project.IsDisposed) { break; }

                if (simulate && !paused) { project.FindSubsystem<SubsystemUpdate>()?.Update(); }

                if (simulate)
                {
                    TickStatistics.Record(
                        System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                        simulationLateness
                    );
                }

                if (_world.IsClosed || project.IsDisposed) { break; }

                if (!paused) { project.FindSubsystem<SubsystemTerrain>()?.Update(0); }

                if (_world.IsClosed || project.IsDisposed) { break; }

                if (now >= nextSave)
                {
                    nextSave = now + 120;
                    WorldProjectPersistence.For(project).SaveAsync();
                }

                _maximumTickMilliseconds = Math.Max(
                    _maximumTickMilliseconds,
                    System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds
                );
                _serviceCycles++;
                Publish(AuthorityExecutionState.Running, paused);
                nextService = now + _servicePeriod;
            }
        }
        catch (OperationCanceledException) when (_loading.IsCancellationRequested)
        {
            _ready.TrySetCanceled(_loading.Token);
        }
        catch (Exception error)
        {
            failure = error;
            _ready.TrySetException(error);
            Log.Error($"Authority world failed: {error}");
        }
        finally
        {
            try { ReleaseWorld(); }
            catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }

            lock (_gate)
            {
                var closed = new ObjectDisposedException(nameof(AuthorityWorldExecutor));
                while (_requests.TryDequeue(out var request)) { request.Reject(closed); }

                _signalDisposed = true;
                _wake.Dispose();
                _loading.Dispose();
            }

            Publish(failure == null ? AuthorityExecutionState.Stopped : AuthorityExecutionState.Faulted, false);
            if (failure == null)
            {
                successfulStop?.Completion.TrySetResult();
                _stop?.Completion.TrySetResult();
                _completion.TrySetResult();
            }
            else
            {
                successfulStop?.Completion.TrySetException(failure);
                _stop?.Completion.TrySetException(failure);
                _completion.TrySetException(failure);
            }
        }
    }

    private void DrainRequests(double until)
    {
        while (_world is { IsClosed: false })
        {
            IRequest request;
            lock (_gate)
            {
                if (!_requests.TryDequeue(out request!)) { return; }
            }

            request.Execute(_world);
            if (Time.RealTime >= until) { return; }
        }
    }

    private void Publish(AuthorityExecutionState state, bool paused)
    {
        var world = _world;
        var node = world == null ? null : WorldNetworkBinding.FindNode(world);
        int pending;
        lock (_gate) { pending = _requests.Count; }

        Volatile.Write(
            ref _status,
            new AuthorityWorldStatus(
                world?.Id ?? Guid.Empty,
                state,
                paused,
                world?.Clock.TickIndex ?? 0,
                _serviceCycles,
                pending,
                node?.IsTransportRunning == true,
                node?.Connections.PlayerCount ?? 0,
                _maximumTickMilliseconds,
                node?.LocalPort,
                world?.Project?.FindSubsystem<SubsystemCharacters>()?.Characters
                    .Where(character => character.ComponentCharacter != null)
                    .Select(character => new AuthorityCharacterStatus(
                            character.CharacterId,
                            character.ComponentCharacter!.ComponentBody.Position
                        )
                    ).ToArray() ??
                []
            )
        );
    }

    private void ReleaseWorld()
    {
        if (_world == null) { return; }

        var project = _world.BoundProject;
        var node = _endpoint;
        var errors = new List<Exception>();
        try { _persistence?.Completion.GetAwaiter().GetResult(); }
        catch (Exception error) { errors.Add(error); }

        try { _world.Close(); }
        catch (Exception error) { errors.Add(error); }

        try { project?.Dispose(); }
        catch (Exception error) { errors.Add(error); }

        try { node?.StopImmediate(); }
        catch (Exception error) { errors.Add(error); }

        if (errors.Count > 0) { throw new AggregateException("Failed to retire authority world.", errors); }
    }

    public ValueTask DisposeAsync() => new(StopAsync(false));

    private sealed class StopRequest(bool save)
    {
        public bool Save { get; } = save;

        public double? DrainStartedAt { get; set; }

        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private interface IRequest
    {
        void Execute(WorldRuntimeContext world);

        void Reject(Exception error);
    }

    private sealed class Request<T>(Func<WorldRuntimeContext, T> operation, CancellationToken cancellation) : IRequest
    {
        private readonly TaskCompletionSource<T> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<T> Task => _result.Task;

        public void Execute(WorldRuntimeContext world)
        {
            if (cancellation.IsCancellationRequested)
            {
                _result.TrySetCanceled(cancellation);
                return;
            }

            try { _result.TrySetResult(operation(world)); }
            catch (Exception error) { _result.TrySetException(error); }
        }

        public void Reject(Exception error) => _result.TrySetException(error);
    }
}
