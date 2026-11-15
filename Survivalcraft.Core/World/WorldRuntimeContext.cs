using System.Runtime.CompilerServices;

using EntitySystem.Core;

using Game.World.Timing;

namespace Game.World;

/// <summary>Explicit world binding and lifetime for one side of a game session.</summary>
public sealed class WorldRuntimeContext
{
    public WorldUpdateClock Clock { get; } = new();

    private int _executionThreadId;

    public void BindExecutionThread()
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        var current = Environment.CurrentManagedThreadId;
        var previous = Interlocked.CompareExchange(ref _executionThreadId, current, 0);
        if (previous != 0 && previous != current)
        {
            throw new InvalidOperationException("The world already belongs to another execution thread.");
        }
    }

    public void VerifyExecutionAccess()
    {
        var owner = Volatile.Read(ref _executionThreadId);
        if (owner != 0 && owner != Environment.CurrentManagedThreadId)
        {
            throw new InvalidOperationException("World operations must execute on their owning thread.");
        }
    }

    private static readonly ConditionalWeakTable<Project, WorldRuntimeContext> _worlds = new();


    private readonly CancellationTokenSource _lifetime = new();


    private readonly List<IDisposable> _worldResources = [];

    public void OwnWorldResource(IDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ObjectDisposedException.ThrowIf(IsClosed, this);
        _worldResources.Add(resource);
    }

    private readonly List<IDisposable> _finalWorldResources = [];

    public void OwnFinalWorldResource(IDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ObjectDisposedException.ThrowIf(IsClosed, this);
        _finalWorldResources.Add(resource);
    }

    private void ReleaseWorldResources()
    {
        var errors = new List<Exception>();
        foreach (var resource in _worldResources.AsEnumerable().Reverse())
        {
            try { resource.Dispose(); }
            catch (Exception exception) { errors.Add(exception); }
        }

        _worldResources.Clear();
        foreach (var resource in _finalWorldResources.AsEnumerable().Reverse())
        {
            try { resource.Dispose(); }
            catch (Exception exception) { errors.Add(exception); }
        }

        _finalWorldResources.Clear();

        if (errors.Count > 0)
        {
            throw new AggregateException("Could not release all world runtime resources.", errors);
        }
    }

    private readonly List<DelayedAction> _delayedActions = [];

    private bool _runningDelayedActions;

    public void QueueTimeDelayedExecution(double time, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!double.IsFinite(time)) { throw new ArgumentOutOfRangeException(nameof(time)); }

        if (IsClosed) { return; }

        _delayedActions.Add(new DelayedAction(time, action));
    }

    public void RunTimeDelayedExecutions(double now)
    {
        if (IsClosed || Project is not { IsDisposed: false } || _runningDelayedActions) { return; }

        var ready = _delayedActions.Where(request => request.Time <= now).ToArray();
        _delayedActions.RemoveAll(request => request.Time <= now);
        _runningDelayedActions = true;
        try
        {
            foreach (var request in ready)
            {
                if (IsClosed || Project is not { IsDisposed: false }) { return; }

                try { request.Action(); }
                catch (Exception exception) { Log.Error(exception); }
            }
        }
        finally
        {
            _runningDelayedActions = false;
        }
    }

    private sealed record DelayedAction(double Time, Action Action);

    private volatile Project? _boundProject;

    public Project? BoundProject { get => _boundProject; private set => _boundProject = value; }

    public bool HasProjectResources => BoundProject is not null;

    public Guid Id { get; } = Guid.NewGuid();

    public WorldRuntimeContext(RuntimeSide side, bool hasPresentation)
    {
        if (side == RuntimeSide.Authority && hasPresentation)
        {
            throw new ArgumentException("Authority worlds cannot own presentation.", nameof(hasPresentation));
        }

        Side = side;
        HasPresentation = hasPresentation;
        Lifetime = _lifetime.Token;
    }

    public RuntimeSide Side { get; }

    public CancellationToken Lifetime { get; }


    public bool IsAuthority => Side == RuntimeSide.Authority;

    public bool HasPresentation { get; }


    private volatile Project? _project;

    public Project? Project { get => _project; private set => _project = value; }

    private volatile bool _closed;

    public bool IsClosed => _closed;

    public static bool IsProjectActive(Project project)
    {
        return !project.IsDisposed && !For(project).IsClosed;
    }

    public static WorldRuntimeContext For(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return _worlds.TryGetValue(project, out var runtime)
            ? runtime
            : throw new InvalidOperationException("The project has no world runtime binding.");
    }

    public void BindProject(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        ObjectDisposedException.ThrowIf(IsClosed, this);
        ObjectDisposedException.ThrowIf(project.IsDisposed, project);
        if (Project is not null && !ReferenceEquals(Project, project))
        {
            throw new InvalidOperationException("A runtime cannot be rebound to a different world.");
        }

        if (ReferenceEquals(Project, project))
        {
            return;
        }

        var owner = _worlds.GetValue(project, _ => this);
        if (!ReferenceEquals(owner, this))
        {
            throw new InvalidOperationException("The project already belongs to another runtime.");
        }

        Project = project;
        BoundProject = project;
        project.Disposed += OnProjectDisposed;
    }

    public void Close()
    {
        if (IsClosed)
        {
            return;
        }

        _closed = true;
        Project = null;
        _delayedActions.Clear();
        try
        {
            _lifetime.Cancel();
        }
        finally
        {
            try
            {
                _lifetime.Dispose();
            }
            finally
            {
                if (BoundProject is null) { ReleaseWorldResources(); }
            }
        }
    }

    private void OnProjectDisposed(Project project)
    {
        project.Disposed -= OnProjectDisposed;
        try
        {
            Close();
        }
        finally
        {
            try
            {
                ReleaseWorldResources();
            }
            finally
            {
                BoundProject = null;
                _worlds.Remove(project);
            }
        }
    }
}
