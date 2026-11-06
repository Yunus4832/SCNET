using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.World.Timing;

namespace Game.Subsystems;

public class SubsystemUpdate : Subsystem
{
    private readonly List<IUpdateable> _sortedUpdatable = [];

    private CharacterSleepPopulationSource _sleepPopulation = null!;

    private SubsystemTime _subsystemTime = null!;

    private readonly Dictionary<IUpdateable, bool> _toAddOrRemove = new();

    private readonly Dictionary<IUpdateable, UpdateableInfo> _updatable = new();

    public int UpdatableCount => _updatable.Count;

    public bool IsLastUpdateInFrame { get; private set; }

    public int UpdatesPerFrame { get; set; }

    public void Update()
    {
        try
        {
            if (Project.IsDisposed) { return; }

            var runtime = WorldRuntimeContext.For(Project);
            if (runtime.IsClosed) { return; }

            runtime.RunTimeDelayedExecutions(Time.FrameStartTime);
            for (var i = 0; i < UpdatesPerFrame; i++)
            {
                if (Project.IsDisposed || runtime.IsClosed)
                {
                    return;
                }

                var updates = _subsystemTime.NextFrame(Time.FrameDuration, _sleepPopulation.ReadPopulation);
                if (Project.IsDisposed || runtime.IsClosed)
                {
                    return;
                }

                UpdatesPerFrame = updates;
                IsLastUpdateInFrame = i == UpdatesPerFrame - 1;
                var flag = false;
                foreach (var item in _toAddOrRemove)
                {
                    if (item.Value)
                    {
                        if (_updatable.ContainsKey(item.Key))
                        {
                            continue;
                        }

                        _updatable.Add(
                            item.Key,
                            new UpdateableInfo
                            {
                                UpdateOrder = item.Key.UpdateOrder
                            }
                        );
                    }
                    else
                    {
                        _updatable.Remove(item.Key);
                    }

                    flag = true;
                }

                _toAddOrRemove.Clear();
                foreach (var updateable in _updatable)
                {
                    var updateOrder = updateable.Key.UpdateOrder;
                    if (updateOrder == updateable.Value.UpdateOrder)
                    {
                        continue;
                    }

                    flag = true;
                    updateable.Value.UpdateOrder = updateOrder;
                }

                if (flag)
                {
                    _sortedUpdatable.Clear();
                    foreach (var key in _updatable.Keys)
                    {
                        _sortedUpdatable.Add(key);
                    }

                    _sortedUpdatable.Sort(Comparer.Instance);
                }

                var dt = MathUtils.Clamp(_subsystemTime.GameTimeDelta, 0f, 0.1f);
                WorldRuleHooks.For(Project).Invoke(new WorldUpdatingContext(Project, dt));
                foreach (var sortedUpdateable in _sortedUpdatable)
                {
                    if (Project.IsDisposed || runtime.IsClosed)
                    {
                        return;
                    }

                    try
                    {
                        sortedUpdateable.Update(dt);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }
            }
        }
        finally
        {
            IsLastUpdateInFrame = false;
        }
    }

    private void AddUpdateable(IUpdateable updateable)
    {
        if (!ShouldScheduleUpdateable(updateable, WorldRuntimeContext.For(Project)))
        {
            return;
        }

        _toAddOrRemove[updateable] = true;
    }

    internal static bool ShouldScheduleUpdateable(IUpdateable updateable, WorldRuntimeContext runtime)
    {
        if (runtime.IsClosed) { return false; }

        return updateable.UpdateSide switch
        {
            UpdateExecutionSide.Disabled => false,
            UpdateExecutionSide.Authority => runtime.IsAuthority,
            UpdateExecutionSide.Client => !runtime.IsAuthority,
            UpdateExecutionSide.Both => true,
            UpdateExecutionSide.Presentation => runtime.HasPresentation,
            _ => throw new InvalidOperationException($"Invalid update side for {updateable.GetType().FullName}.")
        };
    }

    private void RemoveUpdateable(IUpdateable updateable)
    {
        _toAddOrRemove[updateable] = false;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _sleepPopulation = new CharacterSleepPopulationSource(Project.FindSubsystem<SubsystemCharacters>(true)!);
        foreach (var item in Project.FindSubsystems<IUpdateable>())
        {
            AddUpdateable(item);
        }

        UpdatesPerFrame = 1;
    }

    public override void OnEntityAdded(Entity entity)
    {
        WorldRuleHooks.For(Project).Invoke(new EntityAddedContext(Project, entity));
        if (!WorldRuntimeContext.IsProjectActive(Project)) { return; }

        foreach (var item in entity.FindComponents<IUpdateable>())
        {
            if (item != null)
            {
                AddUpdateable(item);
            }
        }
    }

    public override void OnEntityRemoved(Entity entity)
    {
        foreach (var item in entity.FindComponents<IUpdateable>())
        {
            if (item != null)
            {
                RemoveUpdateable(item);
            }
        }
    }

    private class UpdateableInfo
    {
        public UpdateOrder UpdateOrder;
    }

    public class Comparer : IComparer<IUpdateable>
    {
        public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

        public static readonly Comparer Instance = new();

        public int Compare(IUpdateable? u1, IUpdateable? u2)
        {
            if (u1 == null && u2 == null)
            {
                return 0;
            }

            if (u1 is null)
            {
                return -1;
            }

            if (u2 is null)
            {
                return 1;
            }

            var num = u1.UpdateOrder - u2.UpdateOrder;
            if (num != 0)
            {
                return num;
            }

            return u1.GetHashCode() - u2.GetHashCode();
        }
    }
}
