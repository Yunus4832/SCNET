using Game.World.Timing;

namespace Survivalcraft.Core.Test.World;

public sealed class WorldUpdateClockTest
{
    [Fact]
    public void AuthorityAndClientAdvanceIndependently()
    {
        var authority = new WorldUpdateClock();
        var client = new WorldUpdateClock();
        for (var i = 1; i <= 60; i++) { client.Advance(1f / 60f, i / 60d); }

        Assert.Equal(0, authority.TickIndex);
        Assert.Equal(0, authority.ElapsedSeconds);
        for (var i = 1; i <= 20; i++) { authority.Advance(.05f, i / 20d); }

        Assert.Equal(20, authority.TickIndex);
        Assert.Equal(60, client.TickIndex);
        Assert.Equal(.05f, authority.ElapsedSeconds);
        Assert.Equal(1f / 60f, client.ElapsedSeconds);
        Assert.Equal(1d, authority.StartedAt);
        Assert.Equal(1d, client.StartedAt);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidElapsedDoesNotPartiallyAdvance(float elapsed)
    {
        var clock = new WorldUpdateClock();
        clock.Advance(.05f, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(elapsed, 11));

        Assert.Equal(1, clock.TickIndex);
        Assert.Equal(10, clock.StartedAt);
        Assert.Equal(.05f, clock.ElapsedSeconds);
    }

    [Theory]
    [InlineData(9d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidOrReversedStartDoesNotPartiallyAdvance(double startedAt)
    {
        var clock = new WorldUpdateClock();
        clock.Advance(.05f, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(.1f, startedAt));

        Assert.Equal(1, clock.TickIndex);
        Assert.Equal(10, clock.StartedAt);
        Assert.Equal(.05f, clock.ElapsedSeconds);
    }

    [Fact]
    public void PeriodicEventsUseTheWorldIntervalEvenWhenAnotherClockAdvances()
    {
        var authority = new WorldUpdateClock();
        var client = new WorldUpdateClock();
        authority.Advance(.06f, .06);
        Assert.False(authority.PeriodicEvent(.1, 0));
        authority.Advance(.06f, .12);
        Assert.True(authority.PeriodicEvent(.1, 0));

        for (var i = 1; i <= 240; i++) { client.Advance(1f / 240f, i / 240d); }

        Assert.True(authority.PeriodicEvent(.1, 0));
        authority.Advance(.02f, .14);
        Assert.False(authority.PeriodicEvent(.1, 0));
    }
}
