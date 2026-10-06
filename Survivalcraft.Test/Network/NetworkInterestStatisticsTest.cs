using Game.Network;

namespace Survivalcraft.Test.Network;

public sealed class NetworkInterestStatisticsTest
{
    [Fact]
    public void SamplesAccumulateAndPreviousSnapshotRemainsUnchanged()
    {
        var empty = new NetworkInterestStatistics();
        var first = empty.AddSample(2.5, 128);
        var latest = first.AddSample(1, 64).AddSample(4, 0);

        Assert.Equal(default, empty);
        Assert.Equal(new NetworkInterestStatistics(1, 2.5, 2.5, 128), first);
        Assert.Equal(new NetworkInterestStatistics(3, 7.5, 4, 192), latest);
    }

    [Theory]
    [InlineData(-1d, 0L)]
    [InlineData(double.NaN, 0L)]
    [InlineData(double.PositiveInfinity, 0L)]
    [InlineData(0d, -1L)]
    public void InvalidSamplesAreRejected(double milliseconds, long allocatedBytes)
    {
        var statistics = new NetworkInterestStatistics(1, 2, 2, 128);

        Assert.Throws<ArgumentOutOfRangeException>(() => statistics.AddSample(milliseconds, allocatedBytes));
        Assert.Equal(new NetworkInterestStatistics(1, 2, 2, 128), statistics);
    }
}
