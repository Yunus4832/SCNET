namespace Game.Terrains;

/// <summary>Observes sky and season inputs without owning terrain work or residency.</summary>
public sealed class TerrainEnvironmentObserver(
    Terrain terrain,
    SubsystemGameInfo gameInfo,
    SubsystemSky sky
)
{
    private int _lastSkylightValue;

    private readonly FloatCurve _temperatureCurve = new(
        new Vector2(0f, 0f),
        new Vector2(0.125f, 0f),
        new Vector2(0.25f, 0f),
        new Vector2(0.375f, -4f),
        new Vector2(0.5f, -12f),
        new Vector2(0.625f, -24f),
        new Vector2(0.75f, -12f),
        new Vector2(0.875f, -4f),
        new Vector2(1f, 0f)
    );

    /// <summary>
    ///     湿度曲线
    /// </summary>
    private readonly FloatCurve _humidityCurve = new(
        new Vector2(0f, 0f),
        new Vector2(0.25f, 0f),
        new Vector2(0.5f, 0f),
        new Vector2(0.75f, 0f),
        new Vector2(1f, 0f)
    );

    public readonly record struct Changes(bool SunlightChanged, bool AppearanceChanged);

    public Changes Update()
    {
        var sunlightChanged = sky.SkyLightValue != _lastSkylightValue;
        _lastSkylightValue = sky.SkyLightValue;
        var appearanceChanged = false;
        if (Time.PeriodicEvent(1, 0.0))
        {
            var temperature = (int)MathUtils.Round(_temperatureCurve.Sample(gameInfo.WorldSettings.TimeOfYear));
            var humidity = (int)MathUtils.Round(_humidityCurve.Sample(gameInfo.WorldSettings.TimeOfYear));
            if (temperature != terrain.SeasonTemperature || humidity != terrain.SeasonHumidity)
            {
                terrain.SeasonTemperature = temperature;
                terrain.SeasonHumidity = humidity;
                appearanceChanged = true;
            }
        }

        return new Changes(sunlightChanged, appearanceChanged);
    }
}
