namespace Game.Terrains.Rendering;

/// <summary>Shares one soft upload budget across every camera in a client world's frame.</summary>
internal sealed class TerrainUploadScheduler(TimeProvider? timeProvider = null)
{
    public const double FrameBudgetMilliseconds = 4;

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    private readonly Dictionary<(TerrainChunk Chunk, ulong Generation), long> _arrivals = [];

    private readonly HashSet<(TerrainChunk Chunk, ulong Generation)> _seenThisFrame = [];

    private long _sequence;

    private long _frame = long.MinValue;

    private double _spentMilliseconds;

    private bool _preferInitial = true;

    private readonly int[] _oldestTurns = [1, 1];

    internal readonly record struct Candidate(TerrainChunk Chunk, bool Initial, float DistanceSquared);

    public double SpentMilliseconds => _spentMilliseconds;

    public int Upload(long frame, IReadOnlyList<Candidate> candidates, Func<TerrainChunk, bool> upload)
    {
        if (frame != _frame)
        {
            foreach (var stale in _arrivals.Keys.Where(key =>
                         !_seenThisFrame.Contains(key) ||
                         key.Chunk.IsDisposed ||
                         key.Generation != key.Chunk.AllocationGeneration
                     ).ToArray())
            {
                _arrivals.Remove(stale);
            }

            _seenThisFrame.Clear();
            _frame = frame;
            _spentMilliseconds = 0;
        }

        foreach (var candidate in candidates)
        {
            var key = Key(candidate.Chunk);
            _seenThisFrame.Add(key);
            _arrivals.TryAdd(key, ++_sequence);
        }

        if (_spentMilliseconds >= FrameBudgetMilliseconds) { return 0; }

        var started = _time.GetTimestamp();
        var initial = Ordered(candidates.Where(candidate => candidate.Initial));
        var maintenance = Ordered(candidates.Where(candidate => !candidate.Initial));
        var uploaded = 0;
        try
        {
            while ((initial.Count != 0 || maintenance.Count != 0) &&
                   _spentMilliseconds + _time.GetElapsedTime(started).TotalMilliseconds < FrameBudgetMilliseconds)
            {
                var queue = initial.Count == 0 ? maintenance :
                    maintenance.Count == 0 ? initial :
                    _preferInitial ? initial : maintenance;
                var group = ReferenceEquals(queue, initial) ? 0 : 1;
                var selected = _oldestTurns[group] == 0
                    ? queue.MinBy(candidate => _arrivals[Key(candidate.Chunk)])
                    : queue[0];
                _oldestTurns[group] = (_oldestTurns[group] + 1) % 4;
                queue.Remove(selected);
                _preferInitial = !selected.Initial;
                if (!upload(selected.Chunk)) { continue; }

                _arrivals.Remove(Key(selected.Chunk));
                uploaded++;
            }
        }
        finally { _spentMilliseconds += _time.GetElapsedTime(started).TotalMilliseconds; }

        return uploaded;
    }

    public void Clear()
    {
        _arrivals.Clear();
        _seenThisFrame.Clear();
    }

    private static (TerrainChunk Chunk, ulong Generation) Key(TerrainChunk chunk) =>
        (chunk, chunk.AllocationGeneration);

    private List<Candidate> Ordered(IEnumerable<Candidate> candidates)
    {
        return candidates.OrderBy(candidate => candidate.DistanceSquared)
            .ThenBy(candidate => _arrivals[Key(candidate.Chunk)]).ToList();
    }
}
