using Game.Network.Transport;
using Game.Protocol.Terrain;
using Game.Protocol.World;
using Game.Terrains.Distribution;

namespace Survivalcraft.Hosting.Test.Network;

public sealed class OutboundWorkBudgetTest
{
    [Fact]
    public void PayloadBudgetLimitsLargeBaselinesWhileSmallMessagesStillFit()
    {
        var clock = new ManualTimeProvider();
        var large = new OutboundWorkBudget(clock);
        var small = new OutboundWorkBudget(clock);
        var snapshot = new TerrainSnapshotMessage(
            new ClientChunkSnapshot(
                new ChunkAllocationId(new(0, 0), 1),
                1,
                new int[AuthorityChunkSnapshot.CellCount],
                new long[AuthorityChunkSnapshot.ShaftCount]
            )
        );
        var accepted = 0;
        for (var index = 0; index < 20; index++)
        {
            if (large.TryConsume(snapshot)) { accepted++; }

            Assert.True(small.TryConsume(new SubsystemTimePackage(index, 0)));
        }

        Assert.InRange(accepted, 1, 19);
        Assert.True(new OutboundWorkBudget(clock).TryConsume(snapshot));
    }

    [Fact]
    public void ElapsedSliceDefersLaterMessagesButAlwaysAllowsFirstProgress()
    {
        var clock = new ManualTimeProvider();
        var budget = new OutboundWorkBudget(clock);
        clock.Advance();
        Assert.True(budget.TryConsume(new SubsystemTimePackage(0, 0)));
        Assert.False(budget.TryConsume(new SubsystemTimePackage(1, 0)));
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;
        internal void Advance() => _timestamp += TimeSpan.FromMilliseconds(5).Ticks;
    }
}
