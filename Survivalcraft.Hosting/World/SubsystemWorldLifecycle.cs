using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.World;

public sealed class SubsystemWorldLifecycle : Subsystem, IUpdateable
{
    private NetNode? _node;

    private WorldRuntimeContext _world = null!;

    private bool _stopRequested;

    private bool _disposed;

    private bool _stopped;

    private long _requestedCycle;

    private double _requestedAt;

    public WorldProjectPersistence Persistence { get; private set; } = null!;

    public WorldRuntimeContext World => _world;

    public event Action<SubsystemWorldLifecycle>? Stopped;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    public UpdateOrder UpdateOrder => UpdateOrder.SubsystemCharacters + 1;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _world = WorldRuntimeContext.For(Project);
        _node = WorldNetworkBinding.FindNode(Project);
        Persistence = new WorldProjectPersistence(Project);
        Project.Disposed += OnProjectDisposed;
    }

    public void RequestStop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_stopRequested) { return; }

        if (_node is null)
        {
            throw new InvalidOperationException("A world stop requires its actual message endpoint.");
        }

        _stopRequested = true;
        _requestedCycle = _node.DispatchCycle;
        _requestedAt = Time.RealTime;
    }

    public void Update(float dt)
    {
        if (_disposed || !_stopRequested || World.IsClosed || _node is null || _node.DispatchCycle <= _requestedCycle)
        {
            return;
        }

        var timedOut = (Time.RealTime - _requestedAt) * 1000 >= NetworkTransportLimits.DisconnectTimeoutMilliseconds;
        if (_node.PendingPackageCount > 0 && !timedOut) { return; }

        if (_node.PendingPackageCount > 0)
        {
            Log.Warning($"World stop drain deadline reached with {_node.PendingPackageCount} messages pending.");
        }

        _stopRequested = false;
        try { Persistence.Save(true); }
        catch (WorldSaveException) { return; }

        var errors = new List<Exception>();
        try { World.Close(); }
        catch (Exception error) { errors.Add(error); }

        try { Project.Dispose(); }
        catch (Exception error) { errors.Add(error); }

        if (errors.Count > 0)
        {
            Log.Error($"World stop cleanup reported errors: {new AggregateException(errors)}");
        }
    }

    private void OnProjectDisposed(Project project)
    {
        project.Disposed -= OnProjectDisposed;
        if (_stopped || !project.IsDisposed) { return; }

        // The runtime's earlier Project.Disposed handler normally closes the world first.
        // Keep this notification conditional on the actual close, including failed cleanup.
        try { World.Close(); }
        catch (Exception error) { Log.Error($"World stop cleanup reported an error: {error}"); }

        if (!World.IsClosed) { return; }

        _stopped = true;
        _stopRequested = false;
        var stopped = Stopped;
        Stopped = null;
        foreach (var callback in stopped?.GetInvocationList() ?? [])
        {
            try { ((Action<SubsystemWorldLifecycle>)callback)(this); }
            catch (Exception error) { Log.Error($"World stop subscriber failed: {error}"); }
        }
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        try { Persistence?.Dispose(); }
        finally
        {
            // A detached subsystem does not own a future world-stop notification.
            // During Project.Dispose, retain subscribers until all project cleanup has run.
            if (Project is not { IsDisposed: true })
            {
                if (Project != null) { Project.Disposed -= OnProjectDisposed; }

                Stopped = null;
            }
        }
    }
}
