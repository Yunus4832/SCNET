using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemSky : Subsystem, IUpdateable
{
    public const float DawnStart = 0.2f;

    public const float DayStart = 0.3f;

    public const float DuskStart = 0.7f;

    public const float NightStart = 0.8f;

    private static readonly int[] _lightValuesMoonless =
    [
        0,
        3,
        6,
        9,
        12,
        15
    ];

    private static readonly int[] _lightValuesNormal =
    [
        3,
        5,
        8,
        10,
        13,
        15
    ];

    private readonly Random _random = new();

    private double _lastLightningStrikeTime;

    private SubsystemTime _subsystemTime = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemBodies _subsystemBodies = null!;

    public event Action<Vector3>? LightningStrike;

    public SubsystemTimeOfDay SubsystemTimeOfDay = null!;

    public float SkyLightIntensity { get; set; }

    public int MoonPhase { get; set; }

    public int SkyLightValue { get; set; }

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        SubsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        UpdateMoonPhase();
        UpdateLightIntensity();
    }

    public void Update(float dt)
    {
        UpdateMoonPhase();
        UpdateLightIntensity();
    }

    public void MakeLightningStrike(Vector3 targetPosition)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).IsAuthority ||
            !(_subsystemTime.GameTime - _lastLightningStrikeTime > 1.0))
        {
            return;
        }

        _lastLightningStrikeTime = _subsystemTime.GameTime;
        if (_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode == EnvironmentBehaviorMode.Living)
        {
            ApplyLightningDamage(targetPosition);
        }

        NetworkSender.Send(Project, new SubsystemSkyPackage(targetPosition), PackageAudience.Global);
        LightningStrike?.Invoke(targetPosition);
    }

    public void ApplyLightningStrike(Vector3 targetPosition)
    {
        if (WorldRuntimeContext.IsProjectActive(Project) && !WorldRuntimeContext.For(Project).IsAuthority)
        {
            LightningStrike?.Invoke(targetPosition);
        }
    }

    private void ApplyLightningDamage(Vector3 targetPosition)
    {
        var dynamicArray = new DynamicArray<ComponentBody>();
        _subsystemBodies.FindBodiesAroundPoint(new Vector2(targetPosition.X, targetPosition.Z), 4f, dynamicArray);
        for (var i = 0; i < dynamicArray.Count; i++)
        {
            var componentBody = dynamicArray.Array[i];
            if (componentBody.Position.Y > targetPosition.Y - 1.5f &&
                Vector2.Distance(
                    new Vector2(componentBody.Position.X, componentBody.Position.Z),
                    new Vector2(targetPosition.X, targetPosition.Z)
                ) <
                4f)
            {
                componentBody.Entity.FindComponent<ComponentOnFire>()?.SetOnFire(null, _random.Float(12f, 15f));
            }

            var componentCreature = componentBody.Entity.FindComponent<ComponentCreature>();
            if (componentCreature is { CharacterStats: not null })
            {
                componentCreature.CharacterStats.StruckByLightning++;
            }
        }

        var x = Terrain.ToCell(targetPosition.X);
        var num3 = Terrain.ToCell(targetPosition.Y);
        var z = Terrain.ToCell(targetPosition.Z);
        float pressure = _random.Float(0f, 1f) < 0.2f ? 39 : 19;
        Project.FindSubsystem<SubsystemExplosions>(true)!.AddExplosion(x, num3 + 1, z, pressure, false, true);
    }

    public override void Dispose() => LightningStrike = null;

    public void UpdateMoonPhase()
    {
        MoonPhase = ((int)MathUtils.Floor(SubsystemTimeOfDay.Day - 0.5 + 5.0) % 8 + 8) % 8;
    }

    public void UpdateLightIntensity()
    {
        SkyLightIntensity = CalculateLightIntensity(SubsystemTimeOfDay.TimeOfDay);
        if (MoonPhase == 4)
        {
            SkyLightValue = _lightValuesMoonless[(int)MathUtils.Round(MathUtils.Lerp(0f, 5f, SkyLightIntensity))];
        }
        else
        {
            SkyLightValue = _lightValuesNormal[(int)MathUtils.Round(MathUtils.Lerp(0f, 5f, SkyLightIntensity))];
        }
    }

    public float CalculateLightIntensity(float timeOfDay)
    {
        //if (timeOfDay <= 0.2f || timeOfDay > 0.8f)
        //{
        //    return 0f;
        //}
        //if (timeOfDay > 0.2f && timeOfDay <= 0.3f)
        //{
        //    return (timeOfDay - 0.2f) / (71f / (226f * (float)Math.PI));
        //}
        //if (timeOfDay > 0.3f && timeOfDay <= 0.7f)
        //{
        //    return 1f;
        //}
        //return 1f - (timeOfDay - 0.7f) / 0.100000024f;
        if (IntervalUtils.IsBetween(timeOfDay, SubsystemTimeOfDay.NightStart, SubsystemTimeOfDay.DawnStart))
        {
            return 0f;
        }

        if (IntervalUtils.IsBetween(timeOfDay, SubsystemTimeOfDay.DawnStart, SubsystemTimeOfDay.DayStart))
        {
            return IntervalUtils.Interval(SubsystemTimeOfDay.DawnStart, timeOfDay) /
                   SubsystemTimeOfDay.DawnInterval;
        }

        if (IntervalUtils.IsBetween(timeOfDay, SubsystemTimeOfDay.DayStart, SubsystemTimeOfDay.DuskStart))
        {
            return 1f;
        }

        return 1f -
               IntervalUtils.Interval(SubsystemTimeOfDay.DuskStart, timeOfDay) /
               SubsystemTimeOfDay.DuskInterval;
    }
}
