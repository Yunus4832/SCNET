namespace Game.Terrains.Execution;

/// <summary>Bounded per-allocation terrain work with explicit waits and elapsed-time service fairness.</summary>
public sealed class TerrainWorkQueue(TimeProvider? timeProvider = null)
{
    private readonly Lock _sync = new();

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    private readonly Dictionary<TerrainWorkKey, Item> _items = [];

    private readonly Dictionary<TerrainWorkKey, HashSet<Item>> _dependents = [];

    private readonly SortedSet<Item>[] _ready = Enumerable.Range(0, 4)
        .Select(group => new SortedSet<Item>(
                Comparer<Item>.Create((a, b) =>
                    {
                        var distance = a.Distance.CompareTo(b.Distance);
                        if (distance != 0) { return distance; }

                        var derived = (a.Key.Stage == TerrainWorkStage.Derived ? 0 : 1)
                            .CompareTo(b.Key.Stage == TerrainWorkStage.Derived ? 0 : 1);
                        if (group is (int)TerrainWorkPriority.FirstDisplay or (int)TerrainWorkPriority.Maintenance &&
                            derived != 0)
                        {
                            return derived;
                        }

                        return a.Sequence.CompareTo(b.Sequence);
                    }
                )
            )
        )
        .ToArray();

    private readonly SortedSet<Item>[] _ages = Enumerable.Range(0, 4)
        .Select(_ => new SortedSet<Item>(
                Comparer<Item>.Create((a, b) =>
                    {
                        var age = a.Since.CompareTo(b.Since);
                        return age != 0 ? age : a.Sequence.CompareTo(b.Sequence);
                    }
                )
            )
        ).ToArray();

    private readonly double[] _service = new double[4];

    private static readonly double[] _weights = [4, 4, 4, 1];

    private static readonly TerrainWorkStage[] _stages = Enum.GetValues<TerrainWorkStage>();

    private long _sequence;

    private double _virtualTime;

    private readonly bool[] _lastSelectionWasAged = new bool[4];

    private readonly double[] _agingDebtMilliseconds = new double[4];


    public sealed class Item
    {
        internal Item(
            TerrainWorkKey key,
            long revision,
            TerrainWorkPriority priority,
            float distance,
            long since,
            long sequence
        )
        {
            Key = key;
            Revision = revision;
            Priority = priority;
            Distance = distance;
            OwnDistance = distance;
            Since = since;
            Sequence = sequence;
        }

        public TerrainWorkKey Key { get; }

        public long Revision { get; internal set; }

        public TerrainWorkPriority Priority { get; internal set; }

        internal float Distance { get; set; }

        internal float OwnDistance { get; set; }

        internal long Since { get; }

        internal long Sequence { get; }

        internal bool Ready { get; set; }

        internal bool Running { get; set; }

        internal bool Requeued { get; set; }

        internal readonly HashSet<TerrainWorkKey> dependencies = [];
    }

    public (int Total, int Ready, int Waiting) Counts
    {
        get
        {
            lock (_sync)
            {
                var ready = _ready.Sum(group => group.Count);
                return (_items.Count, ready, _items.Values.Count(item => !item.Ready && !item.Running));
            }
        }
    }

    public bool HasDependents(TerrainChunk chunk)
    {
        lock (_sync)
        {
            foreach (var stage in _stages)
            {
                if (_dependents.ContainsKey(new TerrainWorkKey(chunk, stage))) { return true; }
            }

            return false;
        }
    }

    internal TerrainWorkPriority? GetDependentPriority(TerrainChunk chunk)
    {
        lock (_sync)
        {
            TerrainWorkPriority? priority = null;
            foreach (var stage in _stages)
            {
                if (!_dependents.TryGetValue(new TerrainWorkKey(chunk, stage), out var dependents)) { continue; }

                foreach (var dependent in dependents)
                {
                    if (priority == null || dependent.Priority < priority) { priority = dependent.Priority; }
                }
            }

            return priority;
        }
    }

    public void Enqueue(TerrainWorkKey key, long revision, TerrainWorkPriority priority, float distance)
    {
        if (!key.IsCurrent) { return; }

        lock (_sync)
        {
            // Every stage of a chunk shares the current physical distance, including blocked old stages.
            foreach (var stage in _stages)
            {
                if (_items.TryGetValue(new TerrainWorkKey(key.Chunk, stage), out var pending))
                {
                    pending.OwnDistance = distance;
                }
            }

            if (_items.TryGetValue(key, out var item))
            {
                var changed = item.Revision != revision;
                var wasReady = item.Ready;
                RemoveReady(item);
                if (changed) { RemoveWaits(item); }

                item.Revision = revision;
                item.Priority = changed || priority < item.Priority ? priority : item.Priority;
                item.OwnDistance = distance;
                if (item.Running) { item.Requeued |= changed; }
                else if (wasReady || changed) { AddReady(item); }

                RefreshDistances([key.Chunk]);

                return;
            }

            item = new Item(key, revision, priority, distance, _time.GetTimestamp(), ++_sequence);
            _items.Add(key, item);
            AddReady(item);
            RefreshDistances([key.Chunk]);
        }
    }

    public Item? Take()
    {
        lock (_sync)
        {
            while (_ready.Any(group => group.Count != 0))
            {
                var group = Enumerable.Range(0, 4).Where(i => _ready[i].Count != 0).MinBy(i => _service[i]);
                _virtualTime = Math.Max(_virtualTime, _service[group]);
                var oldest = _ages[group].Min!;
                var preferred = _ready[group].Min!;
                var aged = _time.GetElapsedTime(oldest.Since) >= TimeSpan.FromMilliseconds(500);
                // Repay an aging turn with distance-preferred execution time, not just one cheap stage.
                // Otherwise expensive old content slices still dominate a nearby chunk's completion path.
                var selected = aged && !_lastSelectionWasAged[group] && _agingDebtMilliseconds[group] <= 0
                    ? oldest
                    : preferred;

                RemoveReady(selected);
                if (!selected.Key.IsCurrent)
                {
                    RemoveItem(selected);
                    SignalCore(selected.Key);
                    continue;
                }

                _lastSelectionWasAged[group] = !ReferenceEquals(selected, preferred);
                selected.Running = true;
                selected.Requeued = false;
                return selected;
            }

            return null;
        }
    }

    public void RecordCost(TerrainWorkPriority priority, TimeSpan elapsed)
    {
        lock (_sync)
        {
            var group = (int)priority;
            var milliseconds = Math.Max(0.05, elapsed.TotalMilliseconds);
            _service[group] += milliseconds / _weights[group];
            // Each aging override reserves three times its elapsed cost for distance-preferred work.
            // Clamp at zero so idle periods cannot bank credit for a later burst of distant work.
            _agingDebtMilliseconds[group] = Math.Max(
                0,
                _agingDebtMilliseconds[group] + (_lastSelectionWasAged[group] ? 3 * milliseconds : -milliseconds)
            );
        }
    }

    public void Finish(Item item, bool complete)
    {
        lock (_sync)
        {
            if (!_items.TryGetValue(item.Key, out var current) || !ReferenceEquals(current, item)) { return; }

            item.Running = false;
            if (!complete || item.Requeued) { AddReady(item); }
            else
            {
                RemoveItem(item);
                SignalCore(item.Key);
            }
        }
    }

    public void Wait(Item item, IEnumerable<TerrainWorkKey> dependencies)
    {
        var required = dependencies.Distinct().ToArray();
        lock (_sync)
        {
            if (!_items.TryGetValue(item.Key, out var current) || !ReferenceEquals(current, item)) { return; }

            foreach (var dependency in required)
            {
                if (dependency == item.Key || Reaches(dependency, item.Key))
                {
                    throw new InvalidOperationException("Terrain work dependencies must not form a cycle.");
                }
            }

            item.Running = false;
            RemoveWaits(item);
            if (item.Requeued)
            {
                AddReady(item);
                return;
            }

            foreach (var dependency in required)
            {
                item.dependencies.Add(dependency);
                if (!_dependents.TryGetValue(dependency, out var waiting))
                {
                    waiting = [];
                    _dependents.Add(dependency, waiting);
                }

                waiting.Add(item);
            }

            RefreshDistances(required.Select(key => key.Chunk));
        }
    }

    // A waiter on a later stage also needs its earlier stages, but never unrelated later work.
    // Read original distances through the live graph so removal cannot leave cached boosts behind.
    private float DependentDistance(TerrainWorkKey key)
    {
        if (_dependents.Count == 0) { return float.PositiveInfinity; }

        var distance = float.PositiveInfinity;
        var pending = new Stack<TerrainWorkKey>();
        var visited = new HashSet<TerrainWorkKey>();
        pending.Push(key);
        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current)) { continue; }

            foreach (var stage in _stages)
            {
                if (stage < current.Stage ||
                    !_dependents.TryGetValue(new TerrainWorkKey(current.Chunk, stage), out var waiters)) { continue; }

                foreach (var waiter in waiters)
                {
                    distance = Math.Min(distance, waiter.OwnDistance);
                    pending.Push(waiter.Key);
                }
            }
        }

        return distance;
    }

    private void RefreshDistances(IEnumerable<TerrainChunk> chunks)
    {
        var pending = new Stack<TerrainChunk>(chunks);
        var visited = new HashSet<TerrainChunk>();
        while (pending.TryPop(out var chunk))
        {
            if (!visited.Add(chunk)) { continue; }

            foreach (var stage in _stages)
            {
                if (!_items.TryGetValue(new TerrainWorkKey(chunk, stage), out var item)) { continue; }

                var distance = Math.Min(item.OwnDistance, DependentDistance(item.Key));
                if (distance == item.Distance) { continue; }

                var ready = item.Ready;
                RemoveReady(item);
                item.Distance = distance;
                if (ready) { AddReady(item); }

                foreach (var dependency in item.dependencies) { pending.Push(dependency.Chunk); }
            }
        }
    }

    private bool Reaches(TerrainWorkKey start, TerrainWorkKey target)
    {
        var pending = new Stack<TerrainWorkKey>();
        var visited = new HashSet<TerrainWorkKey>();
        pending.Push(start);
        while (pending.TryPop(out var key))
        {
            if (key == target) { return true; }

            if (!visited.Add(key) || !_items.TryGetValue(key, out var item)) { continue; }

            foreach (var dependency in item.dependencies) { pending.Push(dependency); }
        }

        return false;
    }

    public void Signal(TerrainWorkKey key)
    {
        lock (_sync)
        {
            if (_items.TryGetValue(key, out var item) && !item.Ready && !item.Running && item.dependencies.Count == 0)
            {
                AddReady(item);
            }

            SignalCore(key);
        }
    }

    public void Cancel(TerrainChunk chunk)
    {
        lock (_sync)
        {
            var keys = _items.Keys.Concat(_dependents.Keys).Where(key => ReferenceEquals(key.Chunk, chunk)).Distinct()
                .ToArray();
            foreach (var key in keys)
            {
                if (_items.TryGetValue(key, out var item)) { RemoveItem(item); }

                SignalCore(key);
            }
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _items.Clear();
            _dependents.Clear();
            foreach (var ready in _ready) { ready.Clear(); }

            foreach (var ages in _ages) { ages.Clear(); }

            _virtualTime = 0;
            Array.Clear(_lastSelectionWasAged);
            Array.Clear(_agingDebtMilliseconds);
            Array.Clear(_service);
        }
    }

    private void AddReady(Item item)
    {
        if (item.Ready) { return; }

        var group = (int)item.Priority;
        if (_ready[group].Count == 0) { _service[group] = Math.Max(_service[group], _virtualTime); }

        item.Ready = true;
        _ready[group].Add(item);
        _ages[group].Add(item);
    }

    private void RemoveReady(Item item)
    {
        if (!item.Ready) { return; }

        _ready[(int)item.Priority].Remove(item);
        _ages[(int)item.Priority].Remove(item);
        item.Ready = false;
    }

    private void RemoveWaits(Item item)
    {
        if (item.dependencies.Count == 0) { return; }

        var dependencies = item.dependencies.ToArray();
        foreach (var dependency in dependencies)
        {
            if (!_dependents.TryGetValue(dependency, out var waiting)) { continue; }

            waiting.Remove(item);
            if (waiting.Count == 0) { _dependents.Remove(dependency); }
        }

        item.dependencies.Clear();
        RefreshDistances(dependencies.Select(key => key.Chunk));
    }

    private void RemoveItem(Item item)
    {
        RemoveReady(item);
        RemoveWaits(item);
        _items.Remove(item.Key);
    }

    private void SignalCore(TerrainWorkKey key)
    {
        if (!_dependents.Remove(key, out var waiting)) { return; }

        foreach (var item in waiting)
        {
            item.dependencies.Remove(key);
            if (item.dependencies.Count == 0 && !item.Running) { AddReady(item); }
        }

        RefreshDistances([key.Chunk]);
    }
}
