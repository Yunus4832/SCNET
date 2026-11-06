using Game.Diagnostics;

namespace Survivalcraft.Hosting.Test.Diagnostics;

public class ServerTickStatisticsTest
{
    [Fact]
    public void TickCountersSeparateExecutionBudgetFromSchedulerLateness()
    {
        var statistics = new ServerTickStatistics();
        statistics.Record(10, 100);
        statistics.Record(50, 0);
        statistics.Record(60, 25);
        Assert.Equal(3, statistics.Ticks);
        Assert.Equal(1, statistics.OverBudgetTicks);
        Assert.Equal(120, statistics.WorkMilliseconds);
        Assert.Equal(60, statistics.MaximumWorkMilliseconds);
        Assert.Equal(100, statistics.MaximumStartLatenessMilliseconds);
    }
}
