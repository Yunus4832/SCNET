namespace Game.Terrains.Lighting;

/// <summary>World environment applied to spatial light without changing its propagation inputs.</summary>
public readonly record struct TerrainLightEnvironment
{
    private static readonly int[] _moonlessValues = [0, 3, 6, 9, 12, 15];

    private static readonly int[] _normalValues = [3, 5, 8, 10, 13, 15];

    public int SkyLightValue { get; }

    public TerrainLightEnvironment(int skyLightValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)skyLightValue, 15u);
        SkyLightValue = skyLightValue;
    }

    public static TerrainLightEnvironment Capture(SubsystemTimeOfDay time)
    {
        return FromIntensity(CalculateIntensity(time, time.TimeOfDay), CalculateMoonPhase(time.Day));
    }

    public static TerrainLightEnvironment FromIntensity(float intensity, int moonPhase)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(intensity, 0f);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(intensity, 1f);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)moonPhase, 7u);
        if (float.IsNaN(intensity)) { throw new ArgumentOutOfRangeException(nameof(intensity)); }

        var values = moonPhase == 4 ? _moonlessValues : _normalValues;
        return new TerrainLightEnvironment(values[(int)MathUtils.Round(MathUtils.Lerp(0f, 5f, intensity))]);
    }

    public static int CalculateMoonPhase(double day)
    {
        return ((int)MathUtils.Floor(day - 0.5 + 5.0) % 8 + 8) % 8;
    }

    public static float CalculateIntensity(SubsystemTimeOfDay time, float timeOfDay)
    {
        if (IntervalUtils.IsBetween(timeOfDay, time.NightStart, time.DawnStart)) { return 0f; }

        if (IntervalUtils.IsBetween(timeOfDay, time.DawnStart, time.DayStart))
        {
            return IntervalUtils.Interval(time.DawnStart, timeOfDay) / time.DawnInterval;
        }

        if (IntervalUtils.IsBetween(timeOfDay, time.DayStart, time.DuskStart)) { return 1f; }

        return 1f - IntervalUtils.Interval(time.DuskStart, timeOfDay) / time.DuskInterval;
    }
}
