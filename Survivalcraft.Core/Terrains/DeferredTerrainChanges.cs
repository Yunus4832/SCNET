namespace Game.Terrains;

/// <summary>Bounded, coalesced natural-change intents; committed terrain remains owned by the caller.</summary>
internal sealed class DeferredTerrainChanges(
    Func<Point3, DeferredTerrainChanges.Cell?> read,
    Action<Point3, int> apply,
    int capacity = 10000,
    TimeProvider? timeProvider = null
)
{
    internal readonly record struct Cell(int Value, ulong AllocationGeneration);

    private readonly record struct Change(int Value, int RequiredValue, ulong AllocationGeneration);

    private readonly record struct Prepared(Point3 Point, Change Change);

    private readonly int _capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));

    private readonly Dictionary<Point3, Change> _pending = [];

    private readonly Queue<Point3> _order = new();

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    private int _remaining;

    private bool _draining;

    internal int Count => _pending.Count;

    internal bool IsFull => Count >= _capacity;

    internal bool HasActiveBatch => _remaining > 0;

    internal bool TryEnqueue(Point3 point, int value)
    {
        if (!_pending.ContainsKey(point) && IsFull) { return false; }

        var cell = read(point);
        if (cell == null) { return false; }

        Add(point, new Change(value, Terrain.ReplaceLight(cell.Value.Value, 0), cell.Value.AllocationGeneration));
        return true;
    }

    /// <summary>Capacity pressure cannot accept only the crop half of a crop/soil intent.</summary>
    internal bool TryEnqueue(ReadOnlySpan<(Point3 Point, int Value)> changes)
    {
        if (changes.Length > _capacity) { return false; }

        var prepared = changes.Length <= 8
            ? stackalloc Prepared[changes.Length]
            : new Prepared[changes.Length].AsSpan();
        var additions = new HashSet<Point3>();
        for (var i = 0; i < changes.Length; i++)
        {
            var (point, value) = changes[i];
            if (!_pending.ContainsKey(point)) { additions.Add(point); }

            if (Count + additions.Count > _capacity) { return false; }

            var cell = read(point);
            if (cell == null) { return false; }

            prepared[i] = new Prepared(
                point,
                new Change(value, Terrain.ReplaceLight(cell.Value.Value, 0), cell.Value.AllocationGeneration)
            );
        }

        foreach (var item in prepared) { Add(item.Point, item.Change); }

        return true;
    }

    internal void StartBatch()
    {
        if (!_draining && !HasActiveBatch) { _remaining = _order.Count; }
    }

    internal int Drain(int maximumItems, TimeSpan maximumTime)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        if (maximumTime <= TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(maximumTime)); }

        if (_draining) { return 0; }

        var started = _time.GetTimestamp();
        var applied = 0;
        var examined = 0;
        _draining = true;
        try
        {
            while (HasActiveBatch &&
                   examined < maximumItems &&
                   (examined == 0 || _time.GetElapsedTime(started) < maximumTime))
            {
                if (!_order.TryDequeue(out var point))
                {
                    _remaining = 0;
                    break;
                }

                _remaining--;
                examined++;
                if (!_pending.Remove(point, out var change)) { continue; }

                var cell = read(point);
                if (cell is not { } current ||
                    current.AllocationGeneration != change.AllocationGeneration ||
                    Terrain.ReplaceLight(current.Value, 0) != change.RequiredValue) { continue; }

                // Consume before callbacks: reentrant enqueue is a new intent for a later batch.
                apply(point, change.Value);
                applied++;
            }
        }
        finally { _draining = false; }

        return applied;
    }

    internal void Clear()
    {
        _pending.Clear();
        _order.Clear();
        _remaining = 0;
    }

    private void Add(Point3 point, Change change)
    {
        if (!_pending.ContainsKey(point)) { _order.Enqueue(point); }

        _pending[point] = change;
    }
}
