using Game.Network.Chunks;
using Game.Protocol;

namespace Survivalcraft.Hosting.Test.Network;

public sealed class ChunkSendBudgetTest
{
    [Fact]
    public void CountCreditReturnsContinuouslyAndBytesAreSharedWithRepairs()
    {
        var budget = new ChunkSendBudget();
        budget.Advance(0, 4, 352);
        Assert.True(budget.TrySpend(352, true));
        budget.Advance(0.249, 4, 352);
        Assert.False(budget.TrySpend(1, true));
        budget.Advance(0.25, 4, 352);
        Assert.True(budget.TrySpend(88, true));
        Assert.False(budget.TrySpend(1, false));
        budget.Advance(0.5, 4, 352);
        Assert.True(budget.TrySpend(88, false));
    }

    [Fact]
    public void ALongIdleCannotCreateAnUnboundedBurstOrResetQuotaWithinTheSameFrame()
    {
        var budget = new ChunkSendBudget();
        budget.Advance(0, 1000, 1000000);
        budget.Advance(100, 1000, 1000000);
        for (var i = 0; i < ChunkSendBudget.MaximumFragmentsPerUpdate; i++)
        {
            Assert.True(budget.TrySpend(ProtocolLimits.TerrainFragmentPayloadSize, true));
        }

        Assert.False(budget.TrySpend(1, true));
        budget.Advance(100, 1000, 1000000);
        Assert.False(budget.TrySpend(1, true));
        budget.Advance(100.01, 1000, 1000000);
        Assert.True(budget.TrySpend(1, true));
    }

    [Fact]
    public void BackwardsClockAndRateChangesCannotMintNewCredit()
    {
        var budget = new ChunkSendBudget();
        budget.Advance(10, 10, 10000);
        Assert.True(budget.TrySpend(352, true));
        budget.Advance(9, 10, 10000);
        Assert.False(budget.TrySpend(1, false));
        budget.Advance(9.5, 10, 10000);
        Assert.False(budget.TrySpend(1, false));
        budget.Advance(10.1, 10, 10000);
        Assert.True(budget.TrySpend(1, false));
        budget.Advance(20, 1, 1);
        Assert.True(budget.TrySpend(352, true));
        Assert.False(budget.TrySpend(0, true));
    }
}
