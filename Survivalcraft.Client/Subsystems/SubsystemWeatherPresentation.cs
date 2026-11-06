using Engine.Audio;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public sealed class SubsystemWeatherPresentation : Subsystem, IDrawable, IUpdateable
{
    private static readonly int[] _drawOrders = [50];

    private readonly Dictionary<GameWidget, Dictionary<Point2, PrecipitationShaftParticleSystem>> _activeShafts = new();

    private readonly Dictionary<GameWidget, Vector2?> _lastShaftsUpdatePositions = new();

    private readonly List<PrecipitationShaftParticleSystem> _toRemove = [];

    private readonly Random _random = new();

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemAudio _subsystemAudio = null!;

    private Sound _rainSound = null!;

    private float _rainVolumeFactor;

    private float _targetRainSoundVolume;

    public SubsystemWeather Weather { get; private set; } = null!;

    public Color RainColor;

    public Color SnowColor;

    public RainSplashParticleSystem RainSplashParticleSystem { get; private set; } = null!;

    public SnowSplashParticleSystem SnowSplashParticleSystem { get; private set; } = null!;

    public int[] DrawOrders => _drawOrders;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        Weather = Project.FindSubsystem<SubsystemWeather>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _rainSound = _subsystemAudio.CreateSound("Audio/Rain");
        _rainSound.IsLooped = true;
        _rainSound.Volume = 0f;
        RainSplashParticleSystem = new RainSplashParticleSystem(Weather.SubsystemTerrain.Catalog);
        _subsystemParticles.AddParticleSystem(RainSplashParticleSystem);
        SnowSplashParticleSystem = new SnowSplashParticleSystem(Weather.SubsystemTerrain.Catalog);
        _subsystemParticles.AddParticleSystem(SnowSplashParticleSystem);
        for (var i = -7; i <= 7; i++)
        {
            for (var j = -7; j <= 7; j++)
            {
                _rainVolumeFactor += _subsystemAudio.CalculateVolume(MathUtils.Sqrt(i * i + j * j), 1f);
            }
        }
    }

    public void Update(float dt)
    {
        var b = (byte)(255f * MathUtils.Lerp(0.15f, 1f, Weather.SubsystemSky.SkyLightIntensity));
        RainColor = new Color(b, b, b);
        var b2 = (byte)(255f * MathUtils.Lerp(0.15f, 1f, Weather.SubsystemSky.SkyLightIntensity));
        SnowColor = new Color(b2, b2, b2);
        if (Time.PeriodicEvent(0.5, 0.0))
        {
            var num4 = 0f;
            if (Weather.PrecipitationIntensity > 0f)
            {
                var num5 = 0f;
                foreach (var listenerPosition in _subsystemAudio.ListenerPositions)
                {
                    var num6 = Terrain.ToCell(listenerPosition.X) - 5;
                    var num7 = Terrain.ToCell(listenerPosition.Z) - 5;
                    var num8 = Terrain.ToCell(listenerPosition.X) + 5;
                    var num9 = Terrain.ToCell(listenerPosition.Z) + 5;
                    Vector3 vector2 = default;
                    for (var l = num6; l <= num8; l++)
                    {
                        for (var m = num7; m <= num9; m++)
                        {
                            var precipitationShaftInfo = Weather.GetPrecipitationShaftInfo(l, m);
                            if (precipitationShaftInfo is not { Type: PrecipitationType.Rain, Intensity: > 0f })
                            {
                                continue;
                            }

                            vector2.X = l + 0.5f;
                            vector2.Y = MathUtils.Max(precipitationShaftInfo.YLimit, listenerPosition.Y);
                            vector2.Z = m + 0.5f;
                            var num10 = vector2.X - listenerPosition.X;
                            var num11 = 8f * (vector2.Y - listenerPosition.Y);
                            var num12 = vector2.Z - listenerPosition.Z;
                            var distance = MathUtils.Sqrt(num10 * num10 + num11 * num11 + num12 * num12);
                            num5 += _subsystemAudio.CalculateVolume(distance, 1.5f) * precipitationShaftInfo.Intensity;
                        }
                    }
                }

                num4 = MathUtils.Max(num4, num5);
            }

            _targetRainSoundVolume = MathUtils.Saturate(1.5f * num4 / _rainVolumeFactor);
        }

        _rainSound.Volume = MathUtils.Saturate(
            MathUtils.Lerp(
                _rainSound.Volume,
                Configuration.ClientConfiguration.Preferences.SoundsVolume * _targetRainSoundVolume,
                5f * dt
            )
        );
        if (_rainSound.Volume > AudioManager.MinAudibleVolume)
        {
            _rainSound.Play();
        }
        else
        {
            _rainSound.Pause();
        }
    }

    public override void Dispose()
    {
        _rainSound?.Dispose();
        if (_subsystemParticles is not null)
        {
            foreach (var shaft in _activeShafts.Values.SelectMany(shafts => shafts.Values))
            {
                if (_subsystemParticles.ContainsParticleSystem(shaft))
                {
                    _subsystemParticles.RemoveParticleSystem(shaft);
                }
            }

            if (RainSplashParticleSystem is not null &&
                _subsystemParticles.ContainsParticleSystem(RainSplashParticleSystem))
            {
                _subsystemParticles.RemoveParticleSystem(RainSplashParticleSystem);
            }

            if (SnowSplashParticleSystem is not null &&
                _subsystemParticles.ContainsParticleSystem(SnowSplashParticleSystem))
            {
                _subsystemParticles.RemoveParticleSystem(SnowSplashParticleSystem);
            }
        }

        _activeShafts.Clear();
        _lastShaftsUpdatePositions.Clear();
        _toRemove.Clear();
    }

    public void Draw(Camera camera, int drawOrder)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        var num = Configuration.ClientConfiguration.Preferences.VisibilityRange > 128 ? 9 :
            Configuration.ClientConfiguration.Preferences.VisibilityRange <= 64 ? 7 : 8;
        var num2 = num * num;
        var activeShafts = GetActiveShafts(camera.GameWidget);
        var vector = new Vector2(camera.ViewPosition.X, camera.ViewPosition.Z);
        var point = Terrain.ToCell(vector);
        _lastShaftsUpdatePositions.TryGetValue(camera.GameWidget, out var value);
        if (value.HasValue && !(Vector2.DistanceSquared(value.Value, vector) > 1f))
        {
            return;
        }

        _lastShaftsUpdatePositions[camera.GameWidget] = vector;
        _toRemove.Clear();
        foreach (var value2 in activeShafts.Values)
        {
            if (MathUtils.Sqr(value2.Point.X + 0.5f - vector.X) + MathUtils.Sqr(value2.Point.Y + 0.5f - vector.Y) >
                num2 + 1f)
            {
                _toRemove.Add(value2);
            }
        }

        foreach (var item in _toRemove)
        {
            if (_subsystemParticles.ContainsParticleSystem(item))
            {
                _subsystemParticles.RemoveParticleSystem(item);
            }

            activeShafts.Remove(item.Point);
        }

        for (var i = point.X - num; i <= point.X + num; i++)
        {
            for (var j = point.Y - num; j <= point.Y + num; j++)
            {
                if (!(MathUtils.Sqr(i + 0.5f - vector.X) + MathUtils.Sqr(j + 0.5f - vector.Y) <= num2))
                {
                    continue;
                }

                var point2 = new Point2(i, j);
                if (activeShafts.ContainsKey(point2))
                {
                    continue;
                }

                var precipitationShaftParticleSystem = new PrecipitationShaftParticleSystem(
                    camera.GameWidget,
                    this,
                    _random,
                    point2,
                    Weather.GetPrecipitationShaftInfo(point2.X, point2.Y).Type
                );
                _subsystemParticles.AddParticleSystem(precipitationShaftParticleSystem);
                activeShafts.Add(point2, precipitationShaftParticleSystem);
            }
        }
    }

    private Dictionary<Point2, PrecipitationShaftParticleSystem> GetActiveShafts(GameWidget gameWidget)
    {
        if (_activeShafts.TryGetValue(gameWidget, out var value))
        {
            return value;
        }

        value = new Dictionary<Point2, PrecipitationShaftParticleSystem>();
        _activeShafts.Add(gameWidget, value);

        return value;
    }
}
