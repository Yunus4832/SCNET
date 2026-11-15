using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.World;

public sealed class SubsystemWorldLifecycle : Subsystem
{
    private NetNode? _node;

    private WorldRuntimeContext _world = null!;

    private bool _disposed;

    private bool _stopped;

    private Func<Task>? _requestStop;

    private Task? _stopping;

    public WorldProjectPersistence Persistence { get; private set; } = null!;

    public WorldRuntimeContext World => _world;

    public event Action<SubsystemWorldLifecycle>? Stopped;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _world = WorldRuntimeContext.For(Project);
        _node = WorldNetworkBinding.FindNode(Project);
        Persistence = new WorldProjectPersistence(Project);
        Project.Disposed += OnProjectDisposed;
    }

    public void BindStopRequest(Func<Task> requestStop)
    {
        if (_requestStop != null) { throw new InvalidOperationException("The world already has a stop owner."); }

        _requestStop = requestStop ?? throw new ArgumentNullException(nameof(requestStop));
    }

    public void RequestStop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        World.VerifyExecutionAccess();
        if (_stopping is { IsCompleted: false }) { return; }

        _stopping = (_requestStop ??
                     throw new InvalidOperationException("The authority stop owner is not attached."))();
        _ = _stopping.ContinueWith(
            completed =>
            {
                if (completed.IsFaulted) { Log.Error($"World stop failed: {completed.Exception}"); }
            },
            TaskScheduler.Default
        );
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
