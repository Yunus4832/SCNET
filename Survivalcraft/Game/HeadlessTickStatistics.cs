namespace Game;

/// <summary>Cumulative main-loop counters; recorded and read on the Headless update thread.</summary>
public sealed class HeadlessTickStatistics
{
    public long Ticks { get; private set; }
    public long OverBudgetTicks { get; private set; }
    public double WorkMilliseconds { get; private set; }
    public double MaximumWorkMilliseconds { get; private set; }
    public double MaximumStartLatenessMilliseconds { get; private set; }

    public void Record(double workMilliseconds, double startLatenessMilliseconds)
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
