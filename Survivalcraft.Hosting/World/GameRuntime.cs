using EntitySystem.Core;

using Game.Network.Handling;

namespace Game.World;

/// <summary>Composes an independently executing authority and a caller-scheduled presentation client.</summary>
public sealed class GameRuntime(
    bool allowsLocalPause,
    Func<Action, Task>? dispatchClient = null
) : IAsyncDisposable
{
    private WorldRuntimeContext? _authority;

    private WorldRuntimeContext? _client;

    private NetNode? _clientNode;

    private readonly Lock _lifetimeGate = new();

    private Task? _closing;

    private ClientRetirement? _clientRetirement;

    public AuthorityWorldExecutor? AuthorityExecution { get; private set; }

    /// <summary>World identity only; live authority state is accessed through AuthorityExecution.</summary>
    public WorldRuntimeContext? Authority => Volatile.Read(ref _authority);

    public WorldRuntimeContext? Client => Volatile.Read(ref _client);

    private volatile bool _disposed;

    private volatile bool _changing;

    public bool IsDisposed { get => _disposed; private set => _disposed = value; }

    public bool IsChangingWorlds { get => _changing; private set => _changing = value; }

    public bool IsPaused { get; private set; }

    public bool IsMultiplayer => !IsDisposed &&
                                 !IsChangingWorlds &&
                                 (AuthorityExecution?.Status.Listening == true ||
                                  _client is { IsClosed: false } &&
                                  _clientNode?.Server?.Connection is { IsConnected: true, IsInProcess: false });

    public Task StartAuthorityAsync(Func<CancellationToken, WorldRuntimeContext> initialize)
    {
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (IsChangingWorlds)
            {
                throw new InvalidOperationException("Cannot start an authority while retiring worlds.");
            }

            if (AuthorityExecution != null || _authority != null)
            {
                throw new InvalidOperationException("Release the authority before starting a replacement.");
            }

            AuthorityExecution = new AuthorityWorldExecutor(
                token =>
                {
                    var world = initialize(token);
                    if (!world.IsAuthority)
                    {
                        throw new InvalidOperationException("The authority loader returned a client world.");
                    }

                    Volatile.Write(ref _authority, world);
                    return world;
                },
                allowsLocalPause
            );
            return AuthorityExecution.Ready;
        }
    }

    public WorldRuntimeContext? GetAuthorityForClient(Project project)
    {
        return !IsDisposed &&
               !IsChangingWorlds &&
               ReferenceEquals(_client?.Project, project) &&
               _authority is { IsClosed: false }
            ? _authority
            : null;
    }

    public void AttachClient(WorldRuntimeContext world)
    {
        lock (_lifetimeGate)
        {
            ArgumentNullException.ThrowIfNull(world);
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            ObjectDisposedException.ThrowIf(world.IsClosed, world);
            if (world.IsAuthority)
            {
                throw new ArgumentException("Only presentation clients can be attached by the host.", nameof(world));
            }

            if (IsChangingWorlds) { throw new InvalidOperationException("Cannot attach while retiring worlds."); }

            if (ReferenceEquals(_client, world)) { return; }

            if (_client != null)
            {
                throw new InvalidOperationException("Release the existing client before replacing it.");
            }

            var node = WorldNetworkBinding.FindNode(world);
            if (node != null && ReferenceEquals(node, WorldNetworkBinding.FindNode(_authority)))
            {
                throw new InvalidOperationException("Authority and client worlds require separate endpoints.");
            }

            Volatile.Write(ref _client, world);
            _clientNode = node;
        }
    }

    public void SetLocalPause(bool requested)
    {
        IsPaused = allowsLocalPause &&
                   requested &&
                   AuthorityExecution?.Status is { State: AuthorityExecutionState.Running, Listening: false } &&
                   _client is { IsClosed: false, Project: { IsDisposed: false } };
        AuthorityExecution?.SetLocalPause(IsPaused);
        _client?.Project?.FindSubsystem<SubsystemTime>()?.SetPaused(IsPaused);
    }

    public async Task<NetNode> ConnectLocalClientAsync(
        PlayerAddress subject,
        Guid admissionToken,
        PackageHandlerCatalog handlers,
        Action<NetNode> initializeClient
    )
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (dispatchClient == null || _client != null || IsChangingWorlds || AuthorityExecution is not { } execution)
        {
            throw new InvalidOperationException("Local connection requires a running authority and empty client slot.");
        }

        var client = await execution.InvokeAsync(world =>
            {
                var server = WorldNetworkBinding.For(world).RequireNode();
                var admitted = server.AdmitInProcessPlayer(subject, admissionToken);
                var endpoint = new NetNode(server.Options, server.ClientPresentation, handlers, NodeRole.Client);
                var serverRecord = new Client(null, 0, Guid.NewGuid(), Guid.Empty);
                var self = new Client(null, admitted.ID, admitted.TokenId, admitted.CharacterId)
                {
                    PlayerAddress = subject, State = ClientState.Connected
                };
                endpoint.PrepareInProcessClient(serverRecord, self);
                InProcessMessageConnection.CreatePair(server, admitted, endpoint, serverRecord);
                server.BeginClientBootstrap(admitted);
                return endpoint;
            }
        ).ConfigureAwait(false);
        try
        {
            await DispatchClient(() =>
                {
                    AttachClient(client.Runtime);
                    try { initializeClient(client); }
                    catch
                    {
                        ReleaseClientCore();
                        throw;
                    }
                }
            ).ConfigureAwait(false);
            return client;
        }
        catch
        {
            await DispatchClient(() =>
                {
                    if (ReferenceEquals(_client, client.Runtime)) { ReleaseClientCore(); }
                    else { client.StopImmediate(); }
                }
            ).ConfigureAwait(false);
            throw;
        }
    }

    public void UpdateClient(float elapsedSeconds, double startedAt)
    {
        if (IsDisposed || _client is not { IsClosed: false } client) { return; }

        client.Clock.Advance(elapsedSeconds, startedAt);
        _clientNode?.Update();
        if (!IsChangingWorlds && !IsPaused && client is { IsClosed: false, Project: { IsDisposed: false } project })
        {
            project.FindSubsystem<SubsystemUpdate>()?.Update();
        }
    }

    public Task ReleaseClientAsync()
    {
        ClientRetirement retirement;
        lock (_lifetimeGate)
        {
            if (_clientRetirement != null) { return _clientRetirement.Completion.Task; }

            if (_client == null) { return Task.CompletedTask; }

            _clientRetirement = retirement = new ClientRetirement();
            IsChangingWorlds = true;
        }

        AuthorityExecution?.SetLocalPause(false);
        IsPaused = false;
        _ = RetireClientAsync(retirement);
        return retirement.Completion.Task;
    }

    private async Task RetireClientAsync(ClientRetirement retirement)
    {
        try
        {
            await DispatchClient(() => BeginClientRetirement(retirement)).ConfigureAwait(false);
            Exception? preparationFailure = null;
            try { await retirement.Preparation.ConfigureAwait(false); }
            catch (Exception error) { preparationFailure = error; }

            await DispatchClient(() =>
                {
                    if (retirement.Finished) { return; }

                    if (preparationFailure != null) { retirement.Errors.Add(preparationFailure); }

                    FinishClientRetirement(retirement);
                }
            ).ConfigureAwait(false);
        }
        catch (Exception error) { retirement.Completion.TrySetException(error); }
    }

    private void BeginClientRetirement(ClientRetirement retirement)
    {
        if (retirement.Started) { return; }

        _client?.VerifyExecutionAccess();
        retirement.Started = true;
        retirement.World = _client;
        retirement.Project = _client?.BoundProject;
        retirement.Node = _clientNode;
        _client = null;
        _clientNode = null;
        try { retirement.World?.Close(); }
        catch (Exception error) { retirement.Errors.Add(error); }

        var terrain = retirement.Project is { IsDisposed: false } project
            ? project.FindSubsystem<SubsystemTerrain>()?.QuiesceBackgroundAsync() ?? Task.CompletedTask
            : Task.CompletedTask;
        var transport = retirement.Node?.PrepareTransportStopAsync() ?? Task.CompletedTask;
        retirement.Preparation = Task.WhenAll(terrain, transport);
    }

    private void FinishClientRetirement(ClientRetirement retirement)
    {
        if (retirement.Finished) { return; }

        retirement.World?.VerifyExecutionAccess();
        retirement.Finished = true;
        try { retirement.Project?.Dispose(); }
        catch (Exception error) { retirement.Errors.Add(error); }

        try
        {
            if (retirement.Node is { } node && ReferenceEquals(node.Runtime, retirement.World))
            {
                node.StopImmediate();
            }
        }
        catch (Exception error) { retirement.Errors.Add(error); }

        lock (_lifetimeGate)
        {
            if (ReferenceEquals(_clientRetirement, retirement)) { _clientRetirement = null; }

            if (_closing == null) { IsChangingWorlds = false; }
        }

        if (retirement.Errors.Count == 0) { retirement.Completion.TrySetResult(); }
        else
        {
            retirement.Completion.TrySetException(
                new AggregateException("Failed to retire client world.", retirement.Errors)
            );
        }
    }

    /// <summary>Only for a non-cancellable host teardown after its frame loop is ending.</summary>
    public void ReleaseClientForHostShutdown()
    {
        _client?.VerifyExecutionAccess();
        ClientRetirement retirement;
        lock (_lifetimeGate)
        {
            if (_clientRetirement == null && _client == null) { return; }

            retirement = _clientRetirement ??= new ClientRetirement();
            IsChangingWorlds = true;
        }

        BeginClientRetirement(retirement);
        try { retirement.Preparation.GetAwaiter().GetResult(); }
        catch (Exception error) { retirement.Errors.Add(error); }

        FinishClientRetirement(retirement);
        retirement.Completion.Task.GetAwaiter().GetResult();
    }

    private void ReleaseClientCore()
    {
        // Unpublished connection setup failures have not started presentation work.
        ReleaseClientForHostShutdown();
    }

    private sealed class ClientRetirement
    {
        public WorldRuntimeContext? World { get; set; }

        public Project? Project { get; set; }

        public NetNode? Node { get; set; }

        public Task Preparation { get; set; } = Task.CompletedTask;

        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<Exception> Errors { get; } = [];

        public bool Started { get; set; }

        public bool Finished { get; set; }
    }

    public Task CloseAsync(bool save)
    {
        TaskCompletionSource completion;
        lock (_lifetimeGate)
        {
            if (IsDisposed) { return Task.CompletedTask; }

            if (_closing != null) { return _closing; }

            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _closing = completion.Task;
            IsChangingWorlds = true;
        }

        _ = CloseCoreAsync(save, completion);
        return completion.Task;
    }

    private async Task CloseCoreAsync(bool save, TaskCompletionSource completion)
    {
        Exception? failure = null;
        try
        {
            var errors = new List<Exception>();
            if (AuthorityExecution is { } execution)
            {
                try { await execution.StopAsync(save).ConfigureAwait(false); }
                catch (WorldSaveException) when (execution.Status.State == AuthorityExecutionState.Running)
                {
                    // The final save failed before retirement; preserve both worlds for retry.
                    throw;
                }
                catch (Exception error) { errors.Add(error); }
            }

            try { await ReleaseClientAsync().ConfigureAwait(false); }
            catch (Exception error) { errors.Add(error); }

            _authority = null;
            IsDisposed = true;
            if (errors.Count > 0) { throw new AggregateException("World retirement reported failures.", errors); }
        }
        catch (Exception error) { failure = error; }
        finally
        {
            lock (_lifetimeGate)
            {
                IsChangingWorlds = false;
                _closing = null;
            }
        }

        if (failure == null) { completion.TrySetResult(); }
        else { completion.TrySetException(failure); }
    }

    private Task DispatchClient(Action action)
    {
        if (dispatchClient != null) { return dispatchClient(action); }

        if (_client != null) { throw new InvalidOperationException("Client lifetime requires its host dispatcher."); }

        action();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => new(CloseAsync(false));
}
