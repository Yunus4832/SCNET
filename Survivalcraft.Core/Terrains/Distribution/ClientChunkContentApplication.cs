namespace Game.Terrains.Distribution;

/// <summary>Applies owned client deliveries under one world-update budget while the terrain worker is paused.</summary>
public sealed class ClientChunkContentApplication(
    IChunkContentTransport transport,
    ClientChunkContentCoordinator contents,
    ClientTerrainDeltaCoordinator deltas,
    TimeProvider? timeProvider = null
)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    private readonly List<ClientChunkSnapshot> _snapshot = new(1);

    private readonly List<TerrainCellDelta> _delta = new(1);

    private readonly List<ChunkContentResync> _resync = new(1);

    private bool _preferSnapshot = true;

    public int Pump(int maximumOperations = 256, TimeSpan? maximumTime = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOperations);
        var budget = maximumTime ?? TimeSpan.FromMilliseconds(4);
        if (budget <= TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(maximumTime)); }

        var started = _time.GetTimestamp();
        var processed = 0;
        while (processed < maximumOperations && (processed == 0 || _time.GetElapsedTime(started) < budget))
        {
            _resync.Clear();
            if (transport.DrainResyncRequests(_resync, 1) > 0)
            {
                deltas.RequireSnapshot(_resync[0]);
            }
            else
            {
                // Recovery floors precede baselines; snapshots and ordinary deltas share the remaining turns.
                var applied = _preferSnapshot ? TrySnapshot() || TryDelta() : TryDelta() || TrySnapshot();
                if (!applied) { break; }

                _preferSnapshot = !_preferSnapshot;
            }

            processed++;
        }

        return processed;
    }

    private bool TrySnapshot()
    {
        _snapshot.Clear();
        if (transport.DrainReceived(_snapshot, 1) == 0) { return false; }

        contents.TryInstall(_snapshot[0]);
        _snapshot.Clear();
        return true;
    }

    private bool TryDelta()
    {
        _delta.Clear();
        if (transport.DrainDeltas(_delta, 1) == 0) { return false; }

        deltas.Receive(_delta[0]);
        return true;
    }
}
