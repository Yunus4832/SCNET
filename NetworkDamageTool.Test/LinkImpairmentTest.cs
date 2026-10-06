namespace NetworkDamageTool.Test;

public sealed class LinkImpairmentTest
{
    [Fact]
    public void OutageUsesHalfOpenWindowAndRecoversNormalPolicy()
    {
        var options = new LinkImpairmentOptions(100, 0, 0, 0)
        {
            OutageStart = TimeSpan.FromSeconds(10),
            OutageDuration = TimeSpan.FromSeconds(5)
        };
        var link = new LinkImpairment(options, 1);
        Assert.False(link.Decide(100, TimeSpan.FromSeconds(9.999)).Drop);
        Assert.True(link.Decide(100, TimeSpan.FromSeconds(10)).Drop);
        Assert.True(link.Decide(100, TimeSpan.FromSeconds(14.999)).Drop);
        Assert.Equal(new ImpairmentDecision(false, TimeSpan.FromMilliseconds(100)),
            link.Decide(100, TimeSpan.FromSeconds(15)));
    }

    [Fact]
    public void OutageOptionsRequireDurationAndKeepDirectionsIndependent()
    {
        string[] args = ["run", "--listen", "127.0.0.1:28989", "--target", "127.0.0.1:28987"];
        Assert.Throws<ArgumentException>(() => DamageProxyOptions.Parse(
            [.. args, "--up-outage-start-seconds", "10"]));
        var options = DamageProxyOptions.Parse(
            [.. args, "--up-outage-start-seconds", "10", "--up-outage-duration-seconds", "5"]);
        Assert.Equal(TimeSpan.FromSeconds(10), options.Upstream.OutageStart);
        Assert.Equal(TimeSpan.FromSeconds(5), options.Upstream.OutageDuration);
        Assert.Equal(TimeSpan.Zero, options.Downstream.OutageDuration);
    }

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
