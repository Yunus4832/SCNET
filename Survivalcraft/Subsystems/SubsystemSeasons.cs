using Engine.Graphics;
using Engine.Media;
using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Subsystems;

public class SubsystemSeasons : Subsystem, IUpdateable
{
    private const string _typeName = "SubsystemSeasons";

    private static Image? _seasonsGradient;

    public const float SummerStart = 0f;

    public const float AutumnStart = 0.25f;

    public const float WinterStart = 0.5f;

    public const float SpringStart = 0.75f;

    public static readonly float MidSummer = IntervalUtils.Midpoint(SummerStart, AutumnStart);

    public static readonly float MidAutumn = IntervalUtils.Midpoint(AutumnStart, WinterStart);

    public static readonly float MidWinter = IntervalUtils.Midpoint(WinterStart, SpringStart);

    public static readonly float MidSpring = IntervalUtils.Midpoint(SpringStart, SummerStart);

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private int _seasonIndex;

    public Season Season { get; set; }

    public float TimeOfSeason { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (CommonLib.WorkType == WorkType.Client) //如果是客户端，不执行更新季节
        {
            return;
        }

        TimeOfYearToSeason(_subsystemGameInfo.WorldSettings.TimeOfYear, out var season, out var timeOfSeason);
        Season = season;
        TimeOfSeason = timeOfSeason;
        _seasonIndex = (int)Season;
        if (Time.PeriodicEvent(10, 0.0) && CommonLib.WorkType == WorkType.Server) //服务器每秒更新一次传递给客户端季节信息
        {
            CommonLib.Net.QueuePackage(new SubsystemSeasonPackage(_seasonIndex, TimeOfSeason));
        }
    }

    public static string GetTimeOfYearName(float timeOfYear)
    {
        TimeOfYearToSeason(timeOfYear, out var season, out var timeOfSeason);
        var num = timeOfSeason switch
        {
            < 0.25f => 0,
            >= 0.75f => 2,
            _ => 1
        };
        return LanguageControl.Get(_typeName, (int)season * 3 + num);
    }


    public static Color GetTimeOfYearColor(float timeOfYear)
    {

        _seasonsGradient ??= (Image)ContentManager.Get<Texture2D>("Textures/Gui/SeasonsSlider").Tag!;
        var x = (int)MathUtils.Clamp(MathUtils.Round(timeOfYear * _seasonsGradient.Width), 0f,
            _seasonsGradient.Width - 1);
        return _seasonsGradient.GetPixel(x, 0);
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        var seasonsGradient = (Image?)ContentManager.Get<Texture2D>("Textures/Gui/SeasonsSlider").Tag;
        _seasonsGradient = seasonsGradient ?? throw new InvalidOperationException("SeasonGradient is not initialized");
    }

    private static void TimeOfYearToSeason(float timeOfYear, out Season season, out float timeOfSeason)
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
