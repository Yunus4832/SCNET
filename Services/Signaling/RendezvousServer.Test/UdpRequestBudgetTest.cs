using System.Net;

namespace RendezvousServer.Test;

public sealed class UdpRequestBudgetTest
{
    [Fact]
    public void AddressAndTotalLimitsResetAtDeadline()
    {
        var clock = new ManualClock();
        var budget = new UdpRequestBudget(clock, 2, 3);
        Assert.True(budget.TryAccept(IPAddress.Loopback));
        Assert.True(budget.TryAccept(IPAddress.Loopback));
        Assert.False(budget.TryAccept(IPAddress.Loopback));
        Assert.True(budget.TryAccept(IPAddress.Parse("192.0.2.1")));
        Assert.False(budget.TryAccept(IPAddress.Parse("192.0.2.2")));
        clock.Now += TimeSpan.FromSeconds(1);
        Assert.True(budget.TryAccept(IPAddress.Loopback));
        Assert.True(budget.TryAccept(IPAddress.Parse("192.0.2.2")));
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
