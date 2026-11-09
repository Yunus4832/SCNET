using EntitySystem.Core;

using Game.Network.Handling;

namespace Game.World;

/// <summary>Owns the independent world lifetimes on both sides of the current game runtime.</summary>
public sealed class GameRuntime(bool allowsLocalPause) : IDisposable
{
    public bool IsMultiplayer => !IsDisposed &&
                                 !IsChangingWorlds &&
                                 (WorldNetworkBinding.FindNode(_authority)?.IsTransportRunning == true ||
                                  WorldNetworkBinding.FindNode(_client)?.Server?.Connection is
                                      { IsConnected: true, IsInProcess: false });

    public bool IsPaused { get; private set; }

    public void SetLocalPause(bool requested)
    {
        IsPaused = allowsLocalPause &&
                   requested &&
                   _authority is { IsClosed: false, Project: { IsDisposed: false } } &&
                   _client is { IsClosed: false, Project: { IsDisposed: false } } &&
                   WorldNetworkBinding.FindNode(_authority)?.IsTransportRunning != true;
        _authority?.Project?.FindSubsystem<SubsystemTime>()?.SetPaused(IsPaused);
        _client?.Project?.FindSubsystem<SubsystemTime>()?.SetPaused(IsPaused);
    }

    private WorldRuntimeContext? _authority;

    private WorldRuntimeContext? _client;

    private NetNode? _authorityNode;

    private NetNode? _clientNode;

    public WorldRuntimeContext? Authority => _authority;

    public WorldRuntimeContext? Client => _client;

    public WorldRuntimeContext? GetAuthorityForClient(Project presentationProject)
    {
        ArgumentNullException.ThrowIfNull(presentationProject);
        return !IsDisposed &&
               !IsChangingWorlds &&
               _client is { IsClosed: false, Project: { IsDisposed: false } } &&
               ReferenceEquals(_client.Project, presentationProject) &&
               _authority is { IsClosed: false, Project: { IsDisposed: false } }
            ? _authority
            : null;
    }

    public bool IsDisposed { get; private set; }

    public bool IsChangingWorlds { get; private set; }

    public void Attach(WorldRuntimeContext runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (IsChangingWorlds)
        {
            throw new InvalidOperationException("Cannot attach a world while releasing its predecessor.");
        }

        ObjectDisposedException.ThrowIf(runtime.IsClosed, runtime);
        ref var slot = ref (runtime.IsAuthority ? ref _authority : ref _client);
        if (ReferenceEquals(slot, runtime)) { return; }

        if (slot is not null)
        {
            throw new InvalidOperationException("Release the existing world before replacing its runtime.");
        }

        var node = WorldNetworkBinding.FindNode(runtime);
        var otherNode = runtime.IsAuthority ? _clientNode : _authorityNode;
        if (node is not null && ReferenceEquals(node, otherNode))
        {
            throw new InvalidOperationException("Authority and client worlds must use separate message nodes.");
        }

        slot = runtime;
        ref var endpoint = ref (runtime.IsAuthority ? ref _authorityNode : ref _clientNode);
        endpoint = node;
    }

    public NetNode ConnectLocalClient(
        PlayerAddress verifiedLocalPlayer,
        Guid admissionToken,
        PackageHandlerCatalog clientHandlers
    )
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_authority is not { IsClosed: false, Project: not null } authority || _client is not null)
        {
            throw new InvalidOperationException("A local client requires an authority world and an empty client slot.");
        }

        var server = WorldNetworkBinding.For(authority).RequireNode();
        var admitted = server.AdmitInProcessPlayer(verifiedLocalPlayer, admissionToken);
        var client = new NetNode(
            server.Options,
            server.ClientPresentation,
            clientHandlers,
            NodeRole.Client
        );
        var serverRecord = new Client(null, 0, Guid.NewGuid(), Guid.Empty);
        var self = new Client(null, admitted.ID, admitted.TokenId, admitted.CharacterId)
        {
            PlayerAddress = verifiedLocalPlayer,
            State = ClientState.Connected
        };
        client.PrepareInProcessClient(serverRecord, self);
        var link = InProcessMessageConnection.CreatePair(server, admitted, client, serverRecord);
        try
        {
            Attach(client.Runtime);
            server.BeginClientBootstrap(admitted);
            return client;
        }
        catch
        {
            link.ToAuthority.Dispose();
            link.ToClient.Dispose();
            ReleaseClient();
            throw;
        }
    }

    public void UpdateMessages()
    {
        if (IsDisposed) { return; }

        if (_authority is { IsClosed: false } authority) { WorldNetworkBinding.FindNode(authority)?.Update(); }

        if (_client is { IsClosed: false } client) { WorldNetworkBinding.FindNode(client)?.Update(); }
    }

    public void UpdateWorlds()
    {
        if (IsDisposed || IsPaused) { return; }

        UpdateWorld(_authority);
        UpdateWorld(_client);
    }

    private static void UpdateWorld(WorldRuntimeContext? runtime)
    {
        if (runtime is { IsClosed: false, Project: { IsDisposed: false } project })
        {
            project.FindSubsystem<SubsystemUpdate>()?.Update();
        }
    }

    public void ReleaseClient()
    {
        SetLocalPause(false);
        IsChangingWorlds = true;
        try { Release(ref _client, ref _clientNode); }
        finally { IsChangingWorlds = false; }
    }

    public void Dispose()
    {
        if (IsDisposed) { return; }

        IsDisposed = true;
        var errors = new List<Exception>();
        try { Release(ref _client, ref _clientNode); }
        catch (Exception exception) { errors.Add(exception); }

        try { Release(ref _authority, ref _authorityNode); }
        catch (Exception exception) { errors.Add(exception); }

        if (errors.Count > 0) { throw new AggregateException("Failed to release game runtime worlds.", errors); }
    }

    private static void Release(ref WorldRuntimeContext? slot, ref NetNode? endpoint)
    {
        var runtime = slot;
        var node = endpoint;
        slot = null;
        endpoint = null;
        if (runtime is null) { return; }

        var project = runtime.BoundProject;
        var errors = new List<Exception>();
        try { runtime.Close(); }
        catch (Exception exception) { errors.Add(exception); }

        try { project?.Dispose(); }
        catch (Exception exception) { errors.Add(exception); }

        try
        {
            if (node != null && ReferenceEquals(node.Runtime, runtime)) { node.StopImmediate(); }
        }
        catch (Exception exception) { errors.Add(exception); }

        if (errors.Count > 0) { throw new AggregateException("Failed to release a world runtime.", errors); }
    }
}
