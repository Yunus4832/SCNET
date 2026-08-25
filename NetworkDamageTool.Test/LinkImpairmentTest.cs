using NetworkDamageTool;

namespace NetworkDamageTool.Test;

public sealed class LinkImpairmentTest
{
    [Fact]
    public void SameSeedProducesSameDecisions()
    {
        var options = new LinkImpairmentOptions(100, 30, 0.25, 512);
        var first = new LinkImpairment(options, 12345);
        var second = new LinkImpairment(options, 12345);

        var firstDecisions = Enumerable.Range(0, 100)
            .Select(index => first.Decide(500 + index, TimeSpan.FromMilliseconds(index * 10)))
            .ToArray();
        var secondDecisions = Enumerable.Range(0, 100)
            .Select(index => second.Decide(500 + index, TimeSpan.FromMilliseconds(index * 10)))
            .ToArray();

        Assert.Equal(firstDecisions, secondDecisions);
    }

    [Fact]
    public void FullLossDropsEveryDatagram()
    {
        var impairment = new LinkImpairment(new LinkImpairmentOptions(0, 0, 1, 0), 1);

        Assert.All(
            Enumerable.Range(0, 20).Select(_ => impairment.Decide(100, TimeSpan.Zero)),
            decision => Assert.True(decision.Drop));
    }

    [Fact]
    public void BandwidthLimitAddsSerializationQueueDelay()
    {
        var impairment = new LinkImpairment(new LinkImpairmentOptions(0, 0, 0, 8), 1);

        var first = impairment.Decide(1_000, TimeSpan.Zero);
        var second = impairment.Decide(1_000, TimeSpan.Zero);

        Assert.Equal(TimeSpan.Zero, first.Delay);
        Assert.Equal(TimeSpan.FromSeconds(1), second.Delay);
    }
}
