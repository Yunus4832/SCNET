namespace Game.World.Timing;

/// <summary>宿主提供的单个世界的实际更新时间，独立于渲染及游戏时间加速。</summary>
public sealed class WorldUpdateClock
{
    public long TickIndex { get; private set; }

    public double StartedAt { get; private set; }

    public float ElapsedSeconds { get; private set; }

    public void Advance(float elapsedSeconds, double startedAt)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        if (!double.IsFinite(startedAt) || startedAt < StartedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startedAt),
                startedAt,
                "World update time must be finite and monotonic."
            );
        }

        var nextTick = checked(TickIndex + 1);
        ElapsedSeconds = elapsedSeconds;
        StartedAt = startedAt;
        TickIndex = nextTick;
    }

    public bool PeriodicEvent(double period, double offset)
    {
        if (!double.IsFinite(period) || period <= 0) { throw new ArgumentOutOfRangeException(nameof(period)); }

        if (!double.IsFinite(offset)) { throw new ArgumentOutOfRangeException(nameof(offset)); }

        var time = StartedAt - offset;
        var boundary = Math.Floor(time / period) * period;
        return time >= boundary && time - ElapsedSeconds < boundary;
    }
}
