namespace Game.Terrains.Lighting;

/// <summary>将世界环境作用于空间光照，保留原有传播输入。</summary>
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
        var values = GetIntensityValues(intensity, moonPhase);
        return new TerrainLightEnvironment(values[(int)MathUtils.Round(MathUtils.Lerp(0f, 5f, intensity))]);
    }

    /// <summary>表现层使用的连续日光曲线，游戏规则仍使用离散光照等级。</summary>
    public static float InterpolateSkyLightValue(float intensity, int moonPhase)
    {
        var values = GetIntensityValues(intensity, moonPhase);
        var position = intensity * (values.Length - 1);
        var lower = (int)position;
        var upper = Math.Min(lower + 1, values.Length - 1);
        return MathUtils.Lerp(values[lower], values[upper], position - lower);
    }

    private static int[] GetIntensityValues(float intensity, int moonPhase)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(intensity, 0f);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(intensity, 1f);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)moonPhase, 7u);
        if (float.IsNaN(intensity)) { throw new ArgumentOutOfRangeException(nameof(intensity)); }

        return moonPhase == 4 ? _moonlessValues : _normalValues;
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
