using Game.Utils;
using Game.World;

namespace Survivalcraft.Core.Test.World;

public sealed class SeasonCalendarTest
{
    [Theory]
    [InlineData(0f, Season.Summer, 0f)]
    [InlineData(0.125f, Season.Summer, 0.5f)]
    [InlineData(0.25f, Season.Autumn, 0f)]
    [InlineData(0.375f, Season.Autumn, 0.5f)]
    [InlineData(0.5f, Season.Winter, 0f)]
    [InlineData(0.625f, Season.Winter, 0.5f)]
    [InlineData(0.75f, Season.Spring, 0f)]
    [InlineData(0.875f, Season.Spring, 0.5f)]
    [InlineData(1f, Season.Summer, 0f)]
    [InlineData(1.25f, Season.Autumn, 0f)]
    [InlineData(-0.125f, Season.Spring, 0.5f)]
    public void QuarterBoundariesAndWrappedYearsRetainTheirSeasonFractions(float year, Season expected, float fraction)
    {
        SeasonCalendar.TimeOfYearToSeason(year, out var season, out var timeOfSeason);
        Assert.Equal(expected, season);
        Assert.Equal(fraction, timeOfSeason);
    }

    [Fact]
    public void CircularIntervalsRetainCrossYearDistancesAndMidpoints()
    {
        Assert.Equal(0.875f, IntervalUtils.Normalize(-0.125f));
        Assert.Equal(0.125f, IntervalUtils.Add(0.875f, 0.25f));
        Assert.Equal(0.25f, IntervalUtils.Interval(0.875f, 0.125f));
        Assert.Equal(0.25f, IntervalUtils.Distance(0.875f, 0.125f));
        Assert.Equal(0f, IntervalUtils.Midpoint(0.875f, 0.125f));
        Assert.True(IntervalUtils.IsBetween(0.99f, 0.875f, 0.125f));
        Assert.False(IntervalUtils.IsBetween(0.5f, 0.875f, 0.125f));
        Assert.Equal(0.125f, SeasonCalendar.MidSummer);
        Assert.Equal(0.375f, SeasonCalendar.MidAutumn);
        Assert.Equal(0.625f, SeasonCalendar.MidWinter);
        Assert.Equal(0.875f, SeasonCalendar.MidSpring);
    }
}
