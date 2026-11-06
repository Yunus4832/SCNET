namespace Game.World;

public static class SeasonCalendar
{
    public const float SummerStart = 0f;

    public const float AutumnStart = 0.25f;

    public const float WinterStart = 0.5f;

    public const float SpringStart = 0.75f;

    public const float MidSummer = 0.125f;

    public static readonly float MidAutumn = IntervalUtils.Midpoint(AutumnStart, WinterStart);

    public static readonly float MidWinter = IntervalUtils.Midpoint(WinterStart, SpringStart);

    public static readonly float MidSpring = IntervalUtils.Midpoint(SpringStart, SummerStart);

    public static void TimeOfYearToSeason(float timeOfYear, out Season season, out float timeOfSeason)
    {
        if (IntervalUtils.IsBetween(timeOfYear, SummerStart, AutumnStart))
        {
            season = Season.Summer;
            timeOfSeason = IntervalUtils.Interval(SummerStart, timeOfYear) /
                           IntervalUtils.Interval(SummerStart, AutumnStart);
        }
        else if (IntervalUtils.IsBetween(timeOfYear, AutumnStart, WinterStart))
        {
            season = Season.Autumn;
            timeOfSeason = IntervalUtils.Interval(AutumnStart, timeOfYear) /
                           IntervalUtils.Interval(AutumnStart, WinterStart);
        }
        else if (IntervalUtils.IsBetween(timeOfYear, WinterStart, SpringStart))
        {
            season = Season.Winter;
            timeOfSeason = IntervalUtils.Interval(WinterStart, timeOfYear) /
                           IntervalUtils.Interval(WinterStart, SpringStart);
        }
        else
        {
            season = Season.Spring;
            timeOfSeason = IntervalUtils.Interval(SpringStart, timeOfYear) /
                           IntervalUtils.Interval(SpringStart, SummerStart);
        }
    }
}
