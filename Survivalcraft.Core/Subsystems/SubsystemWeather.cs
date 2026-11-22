using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemWeather : Subsystem, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    public double FogEndTime;

    public float FogRampTime;

    public double FogStartTime;

    public float LightningIntensity;

    public double PrecipitationEndTime;

    private float _precipitationRampTime;

    public double PrecipitationStartTime;

    private readonly Random _random = new();

    private int[] _shuffledOrder = [];

    private SubsystemBlocksScanner _subsystemBlocksScanner = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemSeasons _subsystemSeasons = null!;

    public float PrecipitationIntensity { get; set; }

    public int FogSeed { get; set; }

    public float FogProgress { get; set; }

    public float FogIntensity { get; set; }

    public SubsystemTerrain SubsystemTerrain { get; set; } = null!;

    public SubsystemSky SubsystemSky { get; set; } = null!;

    public SubsystemTime SubsystemTime { get; set; } = null!;

    /// <summary>
    ///     判断降水是否已经开始。
    ///     如果当前时间在降水开始和结束时间之间（减去渐变时间），返回 true。
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
    ///     判断雾气是否已经开始。
    ///     逻辑与降水类似。
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

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_subsystemGameInfo.TotalElapsedGameTime > PrecipitationEndTime)
            //客户端禁用天气生成
        {
            if (WorldRuntimeContext.For(Project).IsAuthority)
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
                if (WorldRuntimeContext.For(Project).IsAuthority)
                {
                    NetworkSender.Send(
                        Project,
                        WeatherSnapshotBuilder.Capture(this),
                        PackageAudience.Global
                    );
                }
            }
        }

        var secondsFromPrecipitationBoundary = (float)MathUtils.Max(
            0.0,
            MathUtils.Min(
                _subsystemGameInfo.TotalElapsedGameTime - PrecipitationStartTime,
                PrecipitationEndTime - _subsystemGameInfo.TotalElapsedGameTime
            )
        );
        PrecipitationIntensity = _subsystemGameInfo.WorldSettings.AreWeatherEffectsEnabled
            ? MathUtils.Saturate(secondsFromPrecipitationBoundary * 0.04f)
            : 0f;
        if (PrecipitationIntensity.CloseTo(1f) && SubsystemTime.PeriodicGameTimeEvent(1.0, 0.0))
        {
            var allocatedChunks = SubsystemTerrain.Terrain.AllocatedChunks;
            foreach (var _ in allocatedChunks)
            {
                var terrainChunk = allocatedChunks[_random.Int(0, allocatedChunks.Length - 1)];
                if (!terrainChunk.HasCurrentHeightData ||
                    !_random.Bool(LightningIntensity * 0.0002f))
                {
                    continue;
                }

                var searchCenterX = terrainChunk.Origin.X + _random.Int(0, 15);
                var searchCenterZ = terrainChunk.Origin.Y + _random.Int(0, 15);
                Vector3? highestStrikePoint = null;
                for (var j = searchCenterX - 8; j < searchCenterX + 8; j++)
                {
                    for (var k = searchCenterZ - 8; k < searchCenterZ + 8; k++)
                    {
                        var topHeight = SubsystemTerrain.Terrain.GetTopHeight(j, k);
                        if (!highestStrikePoint.HasValue || topHeight > highestStrikePoint.Value.Y)
                        {
                            highestStrikePoint = new Vector3(j, topHeight, k);
                        }
                    }
                }

                if (!highestStrikePoint.HasValue)
                {
                    continue;
                }

                SubsystemSky.MakeLightningStrike(highestStrikePoint.Value);
                return;
            }
        }

        UpdateFog(dt);
    }

    public PrecipitationShaftInfo GetPrecipitationShaftInfo(int x, int z)
    {
        var shaftValue = SubsystemTerrain.Terrain.GetShaftValue(x, z);
        var seasonalTemperature = SubsystemTerrain.Terrain.GetSeasonalTemperature(shaftValue);
        var surfaceHeight = Terrain.ExtractTopHeight(shaftValue);
        PrecipitationShaftInfo result;
        if (IsPlaceFrozen(seasonalTemperature, surfaceHeight))
        {
            result = default;
            result.Intensity = PrecipitationIntensity;
            result.Type = PrecipitationType.Snow;
            result.YLimit = surfaceHeight + 1;
            return result;
        }

        var seasonalHumidity = SubsystemTerrain.Terrain.GetSeasonalHumidity(shaftValue);
        if (seasonalTemperature <= 8 || seasonalHumidity >= 8)
        {
            result = default;
            result.Intensity = PrecipitationIntensity;
            result.Type = PrecipitationType.Rain;
            result.YLimit = surfaceHeight + 1;
            return result;
        }

        result = default;
        result.Intensity = 0f;
        result.Type = PrecipitationType.Rain;
        result.YLimit = surfaceHeight + 1;
        return result;
    }

    public void ManualLightingStrike(Vector3 position, Vector3 direction)
    {
        var targetX = Terrain.ToCell(position.X + direction.X * 32f);
        var targetZ = Terrain.ToCell(position.Z + direction.Z * 32f);
        Vector3? highestStrikePoint = null;
        for (var i = 0; i < 300; i++)
        {
            var offsetX = _random.Int(-8, 8);
            var offsetZ = _random.Int(-8, 8);
            var sampleX = targetX + offsetX;
            var sampleZ = targetZ + offsetZ;
            var surfaceHeight = SubsystemTerrain.Terrain.CalculateTopmostCellHeight(sampleX, sampleZ);
            if (!highestStrikePoint.HasValue || surfaceHeight > highestStrikePoint.Value.Y)
            {
                highestStrikePoint = new Vector3(sampleX, surfaceHeight, sampleZ);
            }
        }

        if (highestStrikePoint.HasValue)
        {
            SubsystemSky.MakeLightningStrike(highestStrikePoint.Value);
        }
    }

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
            if (WorldRuntimeContext.For(Project).IsAuthority)
            {
                var seasonalFogFrequency = _subsystemSeasons.Season is Season.Autumn or Season.Winter
                    ? 1.75f
                    : 1f;
                if (FogEndTime == 0.0 &&
                    _subsystemGameInfo.WorldSettings.StartingPositionMode == StartingPositionMode.Hard)
                {
                    FogStartTime =
                        _subsystemGameInfo.TotalElapsedGameTime +
                        60f * _random.Float(1f, 10f) / seasonalFogFrequency; //1,10
                }
                else
                {
                    FogStartTime =
                        _subsystemGameInfo.TotalElapsedGameTime +
                        60f * _random.Float(10f, 40f) / seasonalFogFrequency; //10,40
                }

                FogEndTime = FogStartTime + 60f * _random.Float(4f, 8f) * seasonalFogFrequency; //4,8
                FogRampTime = _random.Float(20f, 40f);
                FogProgress = 0f;

                Log.Information(
                    $"雾气信息: 开始时间={FogStartTime}, 结束={FogEndTime},当前时间={_subsystemGameInfo.TotalElapsedGameTime}"
                );

                if (WorldRuntimeContext.For(Project).IsAuthority)
                {
                    NetworkSender.Send(
                        Project,
                        WeatherSnapshotBuilder.Capture(this),
                        PackageAudience.Global
                    );
                }
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
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        SubsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemBlocksScanner = Project.FindSubsystem<SubsystemBlocksScanner>(true)!;
        SubsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        SubsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        PrecipitationStartTime = valuesDictionary.GetValue<double>("WeatherStartTime");
        PrecipitationEndTime = valuesDictionary.GetValue<double>("WeatherEndTime");
        LightningIntensity = valuesDictionary.GetValue<float>("LightningIntensity");
        _subsystemSeasons = Project.FindSubsystem<SubsystemSeasons>(true)!;
        _precipitationRampTime = valuesDictionary.GetValue("WeatherRampTime", 25f);
        FogStartTime = valuesDictionary.GetValue<double>("FogStartTime");
        FogEndTime = valuesDictionary.GetValue<double>("FogEndTime");
        FogRampTime = valuesDictionary.GetValue("FogRampTime", 25f);
        FogIntensity = valuesDictionary.GetValue("FogIntensity", 0f);
        FogProgress = valuesDictionary.GetValue("FogProgress", 0f);

        PrecipitationIntensity = valuesDictionary.GetValue("PrecipitationIntensity", 0f);
        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            _subsystemBlocksScanner.ScanningChunkCompleted += OnScanningChunkCompleted;
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        valuesDictionary.SetValue("WeatherStartTime", PrecipitationStartTime);
        valuesDictionary.SetValue("WeatherEndTime", PrecipitationEndTime);
        valuesDictionary.SetValue("LightningIntensity", LightningIntensity);
        valuesDictionary.SetValue("WeatherIntensity", PrecipitationIntensity);
        valuesDictionary.SetValue("WeatherRampTime", _precipitationRampTime);
        valuesDictionary.SetValue("FogStartTime", FogStartTime);
        valuesDictionary.SetValue("FogEndTime", FogEndTime);
        valuesDictionary.SetValue("FogRampTime", FogRampTime);
        valuesDictionary.SetValue("FogIntensity", FogIntensity);
        valuesDictionary.SetValue("FogProgress", FogProgress);
    }

    private void OnScanningChunkCompleted(TerrainChunk chunk)
    {
        if (_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode == EnvironmentBehaviorMode.Living)
        {
            FreezeThawAndDepositSnow(chunk, 0.66f, 0.66f, false);
        }
    }

    public override void Dispose()
    {
        if (_subsystemBlocksScanner is not null)
        {
            _subsystemBlocksScanner.ScanningChunkCompleted -= OnScanningChunkCompleted;
        }
    }

    public void FreezeThawAndDepositSnow(
        TerrainChunk chunk,
        float freezeProbability,
        float thawProbability,
        bool forceDepositSnow
    )
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority) { return; }

        if (_shuffledOrder.Length == 0)
        {
            _shuffledOrder = Enumerable.Range(0, 256).ToArray();
        }

        _shuffledOrder.RandomShuffle(i => _random.Int(i));
        var terrain = SubsystemTerrain.Terrain;
        foreach (var order in _shuffledOrder)
        {
            var localX = order & 0xF;
            var localZ = order >> 4;
            var cellY = chunk.GetTopHeightFast(localX, localZ);
            var cellValueFast = chunk.GetCellValueFast(localX, cellY, localZ);
            var contents = Terrain.ExtractContents(cellValueFast);
            var worldX = chunk.Origin.X + localX;
            var worldY = cellY;
            var worldZ = chunk.Origin.Y + localZ;
            var precipitationShaftInfo = GetPrecipitationShaftInfo(worldX, worldZ);
            if (precipitationShaftInfo.Type == PrecipitationType.Snow)
            {
                if (!_random.Bool(freezeProbability))
                {
                    continue;
                }

                if (contents == 18 &&
                    SubsystemTerrain.Environment.CalculateOceanShoreDistance(worldX, worldZ) >
                    -35f)
                {
                    var cellContents = terrain.GetCellContents(worldX + 1, worldY, worldZ);
                    var cellContents2 = terrain.GetCellContents(worldX - 1, worldY, worldZ);
                    var cellContents3 = terrain.GetCellContents(worldX, worldY, worldZ - 1);
                    var cellContents4 = terrain.GetCellContents(worldX, worldY, worldZ + 1);
                    var hasDryNeighborPositiveX = WorldContentBinding.For(Project).Catalog.FluidBlocks[cellContents] ==
                                                  null &&
                                                  cellContents != 0;
                    var hasDryNeighborNegativeX =
                        WorldContentBinding.For(Project).Catalog.FluidBlocks[cellContents2] ==
                        null &&
                        cellContents2 != 0;
                    var hasDryNeighborNegativeZ =
                        WorldContentBinding.For(Project).Catalog.FluidBlocks[cellContents3] ==
                        null &&
                        cellContents3 != 0;
                    var hasDryNeighborPositiveZ =
                        WorldContentBinding.For(Project).Catalog.FluidBlocks[cellContents4] ==
                        null &&
                        cellContents4 != 0;
                    if (hasDryNeighborPositiveX ||
                        hasDryNeighborNegativeX ||
                        hasDryNeighborNegativeZ ||
                        hasDryNeighborPositiveZ)
                    {
                        SubsystemTerrain.ChangeCell(worldX, worldY, worldZ, Terrain.MakeBlockValue(62));
                    }
                }
                else
                {
                    if ((!forceDepositSnow && !(precipitationShaftInfo.Intensity > 0.5f)) || worldY + 1 >= 255)
                    {
                        continue;
                    }

                    if (SubsystemSnowBlockBehavior.CanSupportSnow(SubsystemTerrain.Catalog, cellValueFast))
                    {
                        if (contents != 62 || ShaftHasSnowOnIce(worldX, worldZ))
                        {
                            SubsystemTerrain.ChangeCell(worldX, worldY + 1, worldZ, Terrain.MakeBlockValue(61));
                        }
                    }
                    else if (SubsystemSnowBlockBehavior.CanBeReplacedBySnow(
                                 SubsystemTerrain.Catalog,
                                 cellValueFast
                             )) //new 新方法，关联到了落叶方块
                    {
                        SubsystemTerrain.ChangeCell(worldX, worldY, worldZ, Terrain.MakeBlockValue(61));
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
                     worldY > 0;
                     cellY--, worldY--, cellValueFast = chunk.GetCellValueFast(localX, cellY, localZ), contents =
                         Terrain.ExtractContents(cellValueFast))
                {
                    switch (contents)
                    {
                        case 61:
                            SubsystemTerrain.DestroyCell(0, worldX, worldY, worldZ, 0, true, true);
                            continue;
                        case 62:
                            SubsystemTerrain.DestroyCell(0, worldX, worldY, worldZ, 0, false, true);
                            continue;
                    }

                    break;
                }
            }
        }
    }
}
