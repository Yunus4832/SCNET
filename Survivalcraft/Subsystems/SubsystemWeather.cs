using Engine.Audio;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public class SubsystemWeather : Subsystem, IDrawable, IUpdateable
{
    private const int _rainSoundRadius = 7;

    private static readonly int[] _drawOrders = [50];

    private readonly Dictionary<GameWidget, Dictionary<Point2, PrecipitationShaftParticleSystem>> _activeShafts = new();

    public double FogEndTime;

    public float FogRampTime;

    public double FogStartTime;

    private readonly Dictionary<GameWidget, Vector2?> _lastShaftsUpdatePositions = new();

    public float LightningIntensity;

    public double PrecipitationEndTime;

    private float _precipitationRampTime;

    public double PrecipitationStartTime;

    private Sound _rainSound = null!;

    private float _rainVolumeFactor;

    private readonly Random _random = new();

    private int[] _shuffledOrder = [];

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemBlocksScanner _subsystemBlocksScanner = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemSeasons _subsystemSeasons = null!;

    private float _targetRainSoundVolume;

    private readonly List<PrecipitationShaftParticleSystem> _toRemove = [];

    public Color RainColor;

    public Color SnowColor;

    public float PrecipitationIntensity { get; set; }

    public int FogSeed { get; set; }

    public float FogProgress { get; set; }

    public float FogIntensity { get; set; }

    public SubsystemTerrain SubsystemTerrain { get; set; } = null!;

    public SubsystemSky SubsystemSky { get; set; } = null!;

    public SubsystemTime SubsystemTime { get; set; } = null!;

    public RainSplashParticleSystem RainSplashParticleSystem { get; set; } = null!;

    public SnowSplashParticleSystem SnowSplashParticleSystem { get; set; } = null!;

    /// <summary>
    /// 判断降水是否已经开始。
    /// 如果当前时间在降水开始和结束时间之间（减去渐变时间），返回 true。
    /// </summary>
    public bool IsPrecipitationStarted
    {
        get
        {
            if (_subsystemGameInfo.TotalElapsedGameTime >= PrecipitationStartTime)
            {
                return _subsystemGameInfo.TotalElapsedGameTime < PrecipitationEndTime - _precipitationRampTime;
            }

            return false;
        }
    }

    /// <summary>
    /// 判断雾气是否已经开始。
    /// 逻辑与降水类似。
    /// </summary>
    public bool IsFogStarted
    {
        get
        {
            if (_subsystemGameInfo.TotalElapsedGameTime >= FogStartTime)
            {
                return _subsystemGameInfo.TotalElapsedGameTime < FogEndTime - FogRampTime;
            }

            return false;
        }
    }

    public int[] DrawOrders => _drawOrders;

    public void Draw(Camera camera, int drawOrder)
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        var num = SettingsManager.Current.VisibilityRange > 128 ? 9 : SettingsManager.Current.VisibilityRange <= 64 ? 7 : 8;
        var num2 = num * num;
        var activeShafts = GetActiveShafts(camera.GameWidget);
        var b = (byte)(255f * MathUtils.Lerp(0.15f, 1f, SubsystemSky.SkyLightIntensity));
        RainColor = new Color(b, b, b);
        var b2 = (byte)(255f * MathUtils.Lerp(0.15f, 1f, SubsystemSky.SkyLightIntensity));
        SnowColor = new Color(b2, b2, b2);
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

            var precipitationShaftParticleSystem = new PrecipitationShaftParticleSystem(camera.GameWidget, this,
                _random, point2, GetPrecipitationShaftInfo(point2.X, point2.Y).Type);
            _subsystemParticles.AddParticleSystem(precipitationShaftParticleSystem);
            activeShafts.Add(point2, precipitationShaftParticleSystem);
        }
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    //↑new
    public void Update(float dt)
    {
        if (_subsystemGameInfo.TotalElapsedGameTime > PrecipitationEndTime)
            //客户端禁用天气生成
        {
            if (CommonLib.WorkType != WorkType.Client)
            {
                if (PrecipitationEndTime == 0.0)
                {
                    if (_subsystemGameInfo.WorldSettings.StartingPositionMode == StartingPositionMode.Hard)
                    {
                        PrecipitationStartTime =
                            _subsystemGameInfo.TotalElapsedGameTime + 60f * _random.Float(2f, 3f);
                        LightningIntensity = _random.Float(0.5f, 1f);
                    }
                    else
                    {
                        PrecipitationStartTime =
                            _subsystemGameInfo.TotalElapsedGameTime + 60f * _random.Float(3f, 6f);
                        LightningIntensity = _random.Float(0.33f, 0.66f);
                    }
                }
                else
                {
                    PrecipitationStartTime = _subsystemGameInfo.TotalElapsedGameTime + 60f * _random.Float(5f, 45f);
                    LightningIntensity = _random.Float(0f, 1f) < 0.5f ? _random.Float(0.33f, 1f) : 0f;
                }

                PrecipitationEndTime = PrecipitationStartTime + 60f * _random.Float(3f, 6f);
                CommonLib.Net.QueuePackage(
                    SubsystemWeatherPackage.CreateSnapshot(this));
            }
        }

        var num = (float)MathUtils.Max(0.0,
            MathUtils.Min(_subsystemGameInfo.TotalElapsedGameTime - PrecipitationStartTime,
                PrecipitationEndTime - _subsystemGameInfo.TotalElapsedGameTime));
        PrecipitationIntensity = _subsystemGameInfo.WorldSettings.AreWeatherEffectsEnabled
            ? MathUtils.Saturate(num * 0.04f)
            : 0f;
        if (PrecipitationIntensity.CloseTo(1f) && SubsystemTime.PeriodicGameTimeEvent(1.0, 0.0))
        {
            var allocatedChunks = SubsystemTerrain.Terrain.AllocatedChunks;
            foreach (var _ in allocatedChunks)
            {
                var terrainChunk = allocatedChunks[_random.Int(0, allocatedChunks.Length - 1)];
                if (terrainChunk.MainThreadState < TerrainChunkState.InvalidVertices1 ||
                    !_random.Bool(LightningIntensity * 0.0002f))
                {
                    continue;
                }

                var num2 = terrainChunk.Origin.X + _random.Int(0, 15);
                var num3 = terrainChunk.Origin.Y + _random.Int(0, 15);
                Vector3? vector = null;
                for (var j = num2 - 8; j < num2 + 8; j++)
                for (var k = num3 - 8; k < num3 + 8; k++)
                {
                    var topHeight = SubsystemTerrain.Terrain.GetTopHeight(j, k);
                    if (!vector.HasValue || topHeight > vector.Value.Y)
                    {
                        vector = new Vector3(j, topHeight, k);
                    }
                }

                if (!vector.HasValue)
                {
                    continue;
                }

                SubsystemSky.MakeLightningStrike(vector.Value);
                return;
            }
        }

        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            UpdateFog(dt);
            return;
        }

        if (Time.PeriodicEvent(0.5, 0.0))
        {
            var num4 = 0f;
            if (PrecipitationIntensity > 0f)
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
                    for (var m = num7; m <= num9; m++)
                    {
                        var precipitationShaftInfo = GetPrecipitationShaftInfo(l, m);
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

                num4 = MathUtils.Max(num4, num5);
            }

            _targetRainSoundVolume = MathUtils.Saturate(1.5f * num4 / _rainVolumeFactor);
        }

        _rainSound.Volume = MathUtils.Saturate(MathUtils.Lerp(_rainSound.Volume,
            SettingsManager.Current.SoundsVolume * _targetRainSoundVolume, 5f * dt));
        if (_rainSound.Volume > AudioManager.MinAudibleVolume)
        {
            _rainSound.Play();
        }
        else
        {
            _rainSound.Pause();
        }

        UpdateFog(dt);
    }

    public PrecipitationShaftInfo GetPrecipitationShaftInfo(int x, int z)
    {
        var shaftValue = SubsystemTerrain.Terrain.GetShaftValue(x, z);
        var seasonalTemperature = SubsystemTerrain.Terrain.GetSeasonalTemperature(shaftValue);
        var num = Terrain.ExtractTopHeight(shaftValue);
        PrecipitationShaftInfo result;
        if (IsPlaceFrozen(seasonalTemperature, num))
        {
            result = default; //new 下方同理，2.4去掉了全球全局降水因素。
            result.Intensity = PrecipitationIntensity;
            result.Type = PrecipitationType.Snow;
            result.YLimit = num + 1;
            return result;
        }

        var seasonalHumidity = SubsystemTerrain.Terrain.GetSeasonalHumidity(shaftValue);
        if (seasonalTemperature <= 8 || seasonalHumidity >= 8)
        {
            result = default;
            result.Intensity = PrecipitationIntensity;
            result.Type = PrecipitationType.Rain;
            result.YLimit = num + 1;
            return result;
        }

        result = default;
        result.Intensity = 0f;
        result.Type = PrecipitationType.Rain;
        result.YLimit = num + 1;
        return result;
    }

    public void ManualLightingStrike(Vector3 position, Vector3 direction)
    {
        var num = Terrain.ToCell(position.X + direction.X * 32f);
        var num2 = Terrain.ToCell(position.Z + direction.Z * 32f);
        Vector3? vector = null;
        for (var i = 0; i < 300; i++)
        {
            var num3 = _random.Int(-8, 8);
            var num4 = _random.Int(-8, 8);
            var num5 = num + num3;
            var num6 = num2 + num4;
            var num7 = SubsystemTerrain.Terrain.CalculateTopmostCellHeight(num5, num6);
            if (!vector.HasValue || num7 > vector.Value.Y)
            {
                vector = new Vector3(num5, num7, num6);
            }
        }

        if (vector.HasValue)
        {
            SubsystemSky.MakeLightningStrike(vector.Value);
        }
    }

    //new↓
    public void ManualPrecipitationStart()
    {
        PrecipitationStartTime = _subsystemGameInfo.TotalElapsedGameTime;
        PrecipitationEndTime = double.PositiveInfinity;
        _precipitationRampTime = 1f;
    }

    public void ManualPrecipitationEnd()
    {
        _precipitationRampTime = 1f;
        PrecipitationEndTime = _subsystemGameInfo.TotalElapsedGameTime + _precipitationRampTime;
    }

    public void ManualFogStart()
    {
        FogStartTime = _subsystemGameInfo.TotalElapsedGameTime;
        FogEndTime = double.PositiveInfinity;
        FogRampTime = 3f;
    }

    public void ManualFogEnd()
    {
        FogRampTime = 3f;
        FogEndTime = _subsystemGameInfo.TotalElapsedGameTime + FogRampTime;
    }


    public static int GetTemperatureAdjustmentAtHeight(int y)
    {
        return (int)MathUtils.Round(y > 64 ? -0.0008f * MathUtils.Sqr(y - 64) : 0.1f * (64 - y));
    }

    public static bool IsPlaceFrozen(int temperature, int y)
    {
        return temperature + GetTemperatureAdjustmentAtHeight(y) <= 0;
    }

    public static bool ShaftHasSnowOnIce(int x, int z)
    {
        return MathUtils.Hash((uint)((x & 0xFFFF) | (z << 16))) > 429496729;
    }

    private void UpdateFog(float dt)
    {
        if (_subsystemGameInfo.TotalElapsedGameTime > FogEndTime)
        {
            if (CommonLib.WorkType != WorkType.Client)
            {
                var num = _subsystemSeasons.Season is Season.Autumn or Season.Winter
                    ? 1.75f
                    : 1f;
                if (FogEndTime == 0.0 &&
                    _subsystemGameInfo.WorldSettings.StartingPositionMode == StartingPositionMode.Hard)
                {
                    FogStartTime =
                        _subsystemGameInfo.TotalElapsedGameTime + 60f * _random.Float(1f, 10f) / num; //1,10
                }
                else
                {
                    FogStartTime =
                        _subsystemGameInfo.TotalElapsedGameTime + 60f * _random.Float(10f, 40f) / num; //10,40
                }

                FogEndTime = FogStartTime + 60f * _random.Float(4f, 8f) * num; //4,8
                FogRampTime = _random.Float(20f, 40f);
                FogProgress = 0f;

                Log.Information(
                    $"雾气信息: 开始时间={FogStartTime}, 结束={FogEndTime},当前时间={_subsystemGameInfo.TotalElapsedGameTime}");

                CommonLib.Net.QueuePackage(
                    SubsystemWeatherPackage.CreateSnapshot(this));
            }
        }

        FogSeed = MathUtils.Hash((int)MathUtils.Remainder(FogStartTime, 1000000.0));
        if (_subsystemGameInfo.WorldSettings.AreWeatherEffectsEnabled)
        {
            if (IsFogStarted)
            {
                FogIntensity = MathUtils.Saturate(FogIntensity + dt / FogRampTime);
                FogProgress += dt / (float)(FogEndTime - FogStartTime);
            }
            else
            {
                FogIntensity = MathUtils.Saturate(FogIntensity - dt / FogRampTime);
            }
        }
        else
        {
            FogIntensity = 0f;
        }
        //NetWork.CommonLib.Net.QueuePackage(new NetWork.Packages.SubsystemWeatherPackage(FogIntensity,FogProgress));
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        SubsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemBlocksScanner = Project.FindSubsystem<SubsystemBlocksScanner>(true)!;
        SubsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        SubsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        PrecipitationStartTime = valuesDictionary.GetValue<double>("WeatherStartTime");
        PrecipitationEndTime = valuesDictionary.GetValue<double>("WeatherEndTime");
        LightningIntensity = valuesDictionary.GetValue<float>("LightningIntensity");
        _subsystemSeasons = Project.FindSubsystem<SubsystemSeasons>(true)!;
        _precipitationRampTime = valuesDictionary.GetValue("WeatherRampTime", 25f); //new2.4
        FogStartTime = valuesDictionary.GetValue<double>("FogStartTime");
        FogEndTime = valuesDictionary.GetValue<double>("FogEndTime");
        FogRampTime = valuesDictionary.GetValue("FogRampTime", 25f);
        FogIntensity = valuesDictionary.GetValue("FogIntensity", 0f);
        FogProgress = valuesDictionary.GetValue("FogProgress", 0f);

        if (RunMode.Value is RunModeType.Gui)
        {
            _rainSound = _subsystemAudio.CreateSound("Audio/Rain");
            _rainSound.IsLooped = true;
            _rainSound.Volume = 0f;
            RainSplashParticleSystem = new RainSplashParticleSystem();
            _subsystemParticles.AddParticleSystem(RainSplashParticleSystem);
            SnowSplashParticleSystem = new SnowSplashParticleSystem();
            _subsystemParticles.AddParticleSystem(SnowSplashParticleSystem);
        }

        PrecipitationIntensity = valuesDictionary.GetValue("PrecipitationIntensity", 0f); //new2.4
        _rainVolumeFactor = 0f;
        if (RunMode.Value is RunModeType.Gui)
        {
            for (var i = -7; i <= 7; i++)
            for (var j = -7; j <= 7; j++)
            {
                var distance = MathUtils.Sqrt(i * i + j * j);
                _rainVolumeFactor += _subsystemAudio.CalculateVolume(distance, 1f);
            }
        }

        _subsystemBlocksScanner.ScanningChunkCompleted += delegate(TerrainChunk chunk)
        {
            if (_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode == EnvironmentBehaviorMode.Living)
            {
                FreezeThawAndDepositSnow(chunk, 0.66f, 0.66f, false); //new,冻结方法有更新
            }
        };
        SubsystemTerrain.TerrainUpdater.ChunkInitialized += delegate(TerrainChunk chunk) //new
        {
            if (_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode != EnvironmentBehaviorMode.Living)
            {
                return;
            }

            FreezeThawAndDepositSnow(chunk, 1f, 1f, _subsystemGameInfo.WorldSettings.AreWeatherEffectsEnabled);
            FreezeThawAndDepositSnow(chunk, 0.66f, 0.66f,
                _subsystemGameInfo.WorldSettings.AreWeatherEffectsEnabled);
        };
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        valuesDictionary.SetValue("WeatherStartTime", PrecipitationStartTime);
        valuesDictionary.SetValue("WeatherEndTime", PrecipitationEndTime);
        valuesDictionary.SetValue("LightningIntensity", LightningIntensity);
        valuesDictionary.SetValue("WeatherIntensity", PrecipitationIntensity); //new
        valuesDictionary.SetValue("WeatherRampTime", _precipitationRampTime);
        valuesDictionary.SetValue("FogStartTime", FogStartTime);
        valuesDictionary.SetValue("FogEndTime", FogEndTime);
        valuesDictionary.SetValue("FogRampTime", FogRampTime);
        valuesDictionary.SetValue("FogIntensity", FogIntensity);
        valuesDictionary.SetValue("FogProgress", FogProgress);
    }

    public Dictionary<Point2, PrecipitationShaftParticleSystem> GetActiveShafts(GameWidget gameWidget)
    {
        if (_activeShafts.TryGetValue(gameWidget, out var value))
        {
            return value;
        }

        value = new Dictionary<Point2, PrecipitationShaftParticleSystem>();
        _activeShafts.Add(gameWidget, value);

        return value;
    }

    public void FreezeThawAndDepositSnow(TerrainChunk chunk, float freezeProbability, float thawProbability,
        bool forceDepositSnow)
    {
        if (_shuffledOrder.Length == 0)
        {
            _shuffledOrder = Enumerable.Range(0, 256).ToArray();
        }

        _shuffledOrder.RandomShuffle(i => _random.Int(i));
        var terrain = SubsystemTerrain.Terrain;
        foreach (var order in _shuffledOrder)
        {
            var num = order & 0xF;
            var num2 = order >> 4;
            var num3 = chunk.GetTopHeightFast(num, num2);
            var cellValueFast = chunk.GetCellValueFast(num, num3, num2);
            var num4 = Terrain.ExtractContents(cellValueFast);
            var num5 = chunk.Origin.X + num;
            var num6 = num3;
            var num7 = chunk.Origin.Y + num2;
            var precipitationShaftInfo = GetPrecipitationShaftInfo(num5, num7);
            if (precipitationShaftInfo.Type == PrecipitationType.Snow)
            {
                if (!_random.Bool(freezeProbability))
                {
                    continue;
                }

                if (num4 == 18 && SubsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(num5, num7) >
                    -35f)
                {
                    var cellContents = terrain.GetCellContents(num5 + 1, num6, num7);
                    var cellContents2 = terrain.GetCellContents(num5 - 1, num6, num7);
                    var cellContents3 = terrain.GetCellContents(num5, num6, num7 - 1);
                    var cellContents4 = terrain.GetCellContents(num5, num6, num7 + 1);
                    var num8 = BlocksManager.FluidBlocks[cellContents] == null && cellContents != 0;
                    var flag = BlocksManager.FluidBlocks[cellContents2] == null && cellContents2 != 0;
                    var flag2 = BlocksManager.FluidBlocks[cellContents3] == null && cellContents3 != 0;
                    var flag3 = BlocksManager.FluidBlocks[cellContents4] == null && cellContents4 != 0;
                    if (num8 || flag || flag2 || flag3)
                    {
                        SubsystemTerrain.ChangeCell(num5, num6, num7, Terrain.MakeBlockValue(62));
                    }
                }
                else
                {
                    if ((!forceDepositSnow && !(precipitationShaftInfo.Intensity > 0.5f)) || num6 + 1 >= 255)
                    {
                        continue;
                    }

                    if (SubsystemSnowBlockBehavior.CanSupportSnow(cellValueFast))
                    {
                        if (num4 != 62 || ShaftHasSnowOnIce(num5, num7))
                        {
                            SubsystemTerrain.ChangeCell(num5, num6 + 1, num7, Terrain.MakeBlockValue(61));
                        }
                    }
                    else if (SubsystemSnowBlockBehavior.CanBeReplacedBySnow(cellValueFast)) //new 新方法，关联到了落叶方块
                    {
                        SubsystemTerrain.ChangeCell(num5, num6, num7, Terrain.MakeBlockValue(61));
                    }
                }
            }
            else
            {
                if (!_random.Bool(thawProbability))
                {
                    continue;
                }

                for (;
                     num6 > 0;
                     num3--, num6--, cellValueFast = chunk.GetCellValueFast(num, num3, num2), num4 =
                         Terrain.ExtractContents(cellValueFast))
                {
                    switch (num4)
                    {
                        case 61:
                            SubsystemTerrain.DestroyCell(0, num5, num6, num7, 0, true, true);
                            continue;
                        case 62:
                            SubsystemTerrain.DestroyCell(0, num5, num6, num7, 0, false, true);
                            continue;
                    }

                    break;
                }
            }
        }
    }
}
