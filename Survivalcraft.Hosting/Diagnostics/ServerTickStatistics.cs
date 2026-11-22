namespace Game.Diagnostics;

/// <summary>主循环累计计数器，在服务器更新线程上记录和读取。</summary>
public sealed class ServerTickStatistics
{
    private readonly Lock _gate = new();

    public long Ticks { get; private set; }

    public long OverBudgetTicks { get; private set; }

    public double WorkMilliseconds { get; private set; }

    public double MaximumWorkMilliseconds { get; private set; }

    public double MaximumStartLatenessMilliseconds { get; private set; }

    public void Record(double workMilliseconds, double startLatenessMilliseconds)
    {
        lock (_gate)
        {
            Ticks++;
            WorkMilliseconds += workMilliseconds;
            MaximumWorkMilliseconds = Math.Max(MaximumWorkMilliseconds, workMilliseconds);
            MaximumStartLatenessMilliseconds = Math.Max(MaximumStartLatenessMilliseconds, startLatenessMilliseconds);
            if (workMilliseconds > 50)
            {
                OverBudgetTicks++;
            }
        }
    }

    public ServerTickSnapshot Capture()
    {
        lock (_gate)
        {
            return new ServerTickSnapshot(
                Ticks,
                OverBudgetTicks,
                WorkMilliseconds,
                MaximumWorkMilliseconds,
                MaximumStartLatenessMilliseconds
            );
        }
    }
}
