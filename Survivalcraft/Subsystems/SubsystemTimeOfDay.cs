using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemTimeOfDay : Subsystem
{
    private const double _offsetFix = 0.30000001192092896;

    private float _dayDuration = 1200;

    public float DayOffset = 0.5f;

    public SubsystemGameInfo SubsystemGameInfo = null!;

    private SubsystemSeasons _subsystemSeasons = null!;

    public float NightOffset = 1f;

    public float SunriseOffset = 0.25f;

    public float SunsetOffset = 0.75f;

    public bool TimeOfDayEnabled = true;

    public float DawnStart { get; private set; }

    public float DayStart { get; private set; }

    public float DuskStart { get; private set; }

    public float NightStart { get; private set; }

    public float DayInterval => IntervalUtils.Interval(DayStart, DuskStart);

    public float DuskInterval => IntervalUtils.Interval(DuskStart, NightStart);

    public float NightInterval => IntervalUtils.Interval(NightStart, DawnStart);

    public float DawnInterval => IntervalUtils.Interval(DawnStart, DayStart);

    public float Midday => IntervalUtils.Midpoint(DayStart, DuskStart);

    public float MidDusk => IntervalUtils.Midpoint(DuskStart, NightStart);

    public float Midnight => IntervalUtils.Midpoint(NightStart, DawnStart);

    public float MidDawn => IntervalUtils.Midpoint(DawnStart, DayStart);

    public float TimeOfDay
    {
        get
        {
            if (!TimeOfDayEnabled)
            {
                return SunsetOffset;
            }

            return SubsystemGameInfo.WorldSettings.TimeOfDayMode switch
            {
                TimeOfDayMode.Changing => CalculateTimeOfDay(),
                TimeOfDayMode.Day => DayOffset,
                TimeOfDayMode.Night => NightOffset,
                TimeOfDayMode.Sunrise => SunriseOffset,
                _ => SunsetOffset
            };
        }
    }

    public double Day => CalculateDay(SubsystemGameInfo.TotalElapsedGameTime);

    public double TimeOfDayOffset { get; set; }

    public static string GetTimeOfDayText(float timeOfDay)
    {
        return timeOfDay switch
        {
            >= 0f and < 0.25f => LanguageControl.Get(ComponentGui.TypeName, 18),
            >= 0.25f and < 0.5f => LanguageControl.Get(ComponentGui.TypeName, 15),
            >= 0.5f and < 0.75f => LanguageControl.Get(ComponentGui.TypeName, 16),
            _ => LanguageControl.Get(ComponentGui.TypeName, 17)
        };
    }

    public double CalculateDay(double totalElapsedGameTime)
    {
        return (totalElapsedGameTime + (TimeOfDayOffset + _offsetFix) * _dayDuration) / _dayDuration;
    }

    public float CalculateTimeOfDay(double totalElapsedGameTime)
    {
        var num = CalculateDay(totalElapsedGameTime);
        return (float)(num - MathUtils.Floor(num));
    }

    public float CalculateTimeOfDay()
    {
        return (float)MathUtils.Remainder(
                   SubsystemGameInfo.TotalElapsedGameTime + (TimeOfDayOffset + _offsetFix) * _dayDuration,
                   _dayDuration) /
               _dayDuration;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        SubsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        TimeOfDayOffset = valuesDictionary.GetValue<double>("TimeOfDayOffset");
        _dayDuration *= SubsystemGameInfo.WorldSettings.DaySpeed;
        _subsystemSeasons = Project.FindSubsystem<SubsystemSeasons>(true)!;
        UpdateStarts();
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        valuesDictionary.SetValue("TimeOfDayOffset", TimeOfDayOffset);
    }

    public virtual void UpdateStarts()
    {
        var num = IntervalUtils.Midpoint(SubsystemSeasons.SummerStart, SubsystemSeasons.AutumnStart);
        var num2 = MathUtils.Remainder(SubsystemGameInfo.WorldSettings.TimeOfYear - num, 1f);
        var num3 = MathUtils.Lerp(0.2f, 0.4f, 0.5f + 0.5f * MathUtils.Cos((float)Math.PI * 2f * num2));
        var num4 = 0.4f;
        var num5 = (1f - (num3 + num4)) / 2f;
        DayStart = 0.3f;
        DawnStart = IntervalUtils.Add(DayStart, 0f - num5);
        DuskStart = IntervalUtils.Add(DayStart, num3);
        NightStart = IntervalUtils.Add(DuskStart, num5);
    }
}
