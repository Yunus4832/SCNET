using Game.Servers;

using LiteNetLib;

namespace Survivalcraft.Test.Servers;

public sealed class ServerDiscoveryServiceTest
{
    [Fact]
    public void PollRetriesLostProbeAndStopsWhenCompleted()
    {
        var network = new NetManager(new EventBasedNetListener());
        var attempts = 1;
        ServerDiscoveryService.Poll(network, TimeSpan.FromSeconds(5), CancellationToken.None,
            () => attempts == 3, () => attempts++);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public void CompletedProbeDoesNotRetry()
    {
        var network = new NetManager(new EventBasedNetListener());
        ServerDiscoveryService.Poll(network, TimeSpan.FromSeconds(5), CancellationToken.None,
            () => true, () => Assert.Fail("Completed probe retried."));
    }

    [Fact]
    public void ShortDeadlineDoesNotRetryAfterTimeout()
    {
        var network = new NetManager(new EventBasedNetListener());
        ServerDiscoveryService.Poll(network, TimeSpan.FromMilliseconds(20), CancellationToken.None,
            () => false, () => Assert.Fail("Expired probe retried."));
    }

    [Fact]
    public void CancellationStopsProbeBeforeRetry()
    {
        var network = new NetManager(new EventBasedNetListener());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            ServerDiscoveryService.Poll(network, TimeSpan.FromSeconds(5), cancellation.Token,
                () => false, () => Assert.Fail("Cancelled probe retried.")));
    }
}
