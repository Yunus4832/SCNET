using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.World;

public sealed class SubsystemWorldLifecycle : Subsystem, IUpdateable
{
    private NetNode? _node;

    private WorldRuntimeContext _world = null!;

    private bool _stopRequested;

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
    }

    public void RequestStop()
    {
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
        if (!_stopRequested || World.IsClosed || _node is null || _node.DispatchCycle <= _requestedCycle) { return; }

        var timedOut = (Time.RealTime - _requestedAt) * 1000 >= NetworkTransportLimits.DisconnectTimeoutMilliseconds;
        if (_node.PendingPackageCount > 0 && !timedOut) { return; }

        if (_node.PendingPackageCount > 0)
        {
            Log.Warning($"World stop drain deadline reached with {_node.PendingPackageCount} messages pending.");
        }

        _stopRequested = false;
        try { Persistence.Save(true); }
        catch (WorldSaveException) { return; }

        var stopped = Stopped;
        var errors = new List<Exception>();
        try { World.Close(); }
        catch (Exception error) { errors.Add(error); }

        try { Project.Dispose(); }
        catch (Exception error) { errors.Add(error); }

        if (errors.Count > 0)
        {
            Log.Error($"World stop cleanup reported errors: {new AggregateException(errors)}");
        }

        if (!World.IsClosed || !Project.IsDisposed) { return; }

        foreach (var callback in stopped?.GetInvocationList() ?? [])
        {
            try { ((Action<SubsystemWorldLifecycle>)callback)(this); }
            catch (Exception error) { Log.Error($"World stop subscriber failed: {error}"); }
        }
    }

    public override void Dispose()
    {
        Persistence?.Dispose();
        Stopped = null;
    }
}
