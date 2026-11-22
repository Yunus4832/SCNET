using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemMetersBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    public const int DiameterBits = 6;

    public const int Diameter = 64;

    public const int DiameterMask = 63;

    public const int Radius = 32;

    private SubsystemSky _subsystemSky = null!;

    private SubsystemTime _subsystemTime = null!;

    private SubsystemWeather _subsystemWeather = null!;

    private readonly Dictionary<Point3, int> _thermometersByPoint = new();

    private readonly DynamicArray<Point3> _thermometersToSimulate = [];

    private int _thermometersToSimulateIndex;

    private readonly DynamicArray<int> _toVisit = [];

    private readonly int[] _visited = new int[8192];

    public override ResourceId[] HandledBlocks =>
        [ResourceId.Parse("game:thermometer"), ResourceId.Parse("game:hygrometer")];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_thermometersToSimulateIndex < _thermometersToSimulate.Count)
        {
            var period = MathUtils.Max(5.0 / _thermometersToSimulate.Count, 1.0);
            if (!_subsystemTime.PeriodicGameTimeEvent(period, 0.0))
            {
                return;
            }

            var point = _thermometersToSimulate.Array[_thermometersToSimulateIndex];
            SimulateThermometer(point.X, point.Y, point.Z, true);
            _thermometersToSimulateIndex++;
        }
        else if (_thermometersByPoint.Count > 0)
        {
            _thermometersToSimulateIndex = 0;
            _thermometersToSimulate.Clear();
            _thermometersToSimulate.AddRange(_thermometersByPoint.Keys);
        }
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var point = CellFace.FaceToPoint3(Terrain.ExtractData(SubsystemTerrain.Terrain.GetCellValue(x, y, z)));
        var cellContents = SubsystemTerrain.Terrain.GetCellContents(x - point.X, y - point.Y, z - point.Z);
        if (WorldContentBinding.For(Project).Catalog.Blocks[cellContents].Transparent)
        {
            SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
        }
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
    {
        AddMeter(value, x, y, z);
    }

    public override void OnBlockRemoved(int value, int oldValue, int x, int y, int z)
    {
        RemoveMeter(oldValue, x, y, z);
    }

    public override void OnBlockModified(int value, int oldValue, int x, int y, int z)
    {
        RemoveMeter(oldValue, x, y, z);
        AddMeter(value, x, y, z);
    }

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
    {
        AddMeter(value, x, y, z);
    }

    public override void OnChunkDiscarding(TerrainChunk chunk)
    {
        var list = new List<Point3>();
        foreach (var key in _thermometersByPoint.Keys)
        {
            if (key.X >= chunk.Origin.X &&
                key.X < chunk.Origin.X + 16 &&
                key.Z >= chunk.Origin.Y &&
                key.Z < chunk.Origin.Y + 16)
            {
                list.Add(key);
            }
        }

        foreach (var item in list)
        {
            _thermometersByPoint.Remove(item);
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemWeather = Project.FindSubsystem<SubsystemWeather>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
    }

    public int GetThermometerReading(int x, int y, int z)
    {
        _thermometersByPoint.TryGetValue(new Point3(x, y, z), out var value);
        return value;
    }

    /// <summary>结合环境、可见热源和保温效果计算温度；热交换权重扣除温度计自身的保温权重。</summary>
    public void CalculateTemperature(
        int x,
        int y,
        int z,
        float meterTemperature,
        float meterInsulation,
        out float temperature,
        out float temperatureFlux
    )
    {
        _toVisit.Clear();
        for (var i = 0; i < _visited.Length; i++)
        {
            _visited[i] = 0;
        }

        var blockTemperatureSum = 0f;
        var blockWeightSum = 0f;
        var outdoorTemperatureSum = 0f;
        var outdoorWeightSum = 0f;
        var heaterTemperatureSum = 0f;
        var heaterWeightSum = 0f;

        // 每个坐标分量占六位、偏置为 32，因此零偏移编码为 133152。
        _toVisit.Add(133152);

        for (var j = 0; j < _toVisit.Count; j++)
        {
            var packedOffset = _toVisit.Array[j];
            if ((_visited[packedOffset / 32] & (1 << packedOffset)) != 0)
            {
                continue;
            }

            _visited[packedOffset / 32] |= 1 << packedOffset;

            var offsetX = (packedOffset & 0x3F) - 32;
            var offsetY = ((packedOffset >> 6) & 0x3F) - 32;
            var offsetZ = ((packedOffset >> 12) & 0x3F) - 32;
            var worldX = offsetX + x;
            var worldY = offsetY + y;
            var worldZ = offsetZ + z;

            var terrain = SubsystemTerrain.Terrain;
            var chunkAtCell = terrain.GetChunkAtCell(worldX, worldZ, false);
            if (chunkAtCell == null || worldY < 0 || worldY >= 256)
            {
                continue;
            }

            var x2 = worldX & 0xF;
            var y2 = worldY;
            var z2 = worldZ & 0xF;
            var cellValueFast = chunkAtCell.GetCellValueFast(x2, y2, z2);
            var contents = Terrain.ExtractContents(cellValueFast);
            var block = WorldContentBinding.For(Project).Catalog.Blocks[contents];

            var heat = GetHeat(cellValueFast);
            if (heat > 0f)
            {
                var distance = MathUtils.Abs(offsetX) + MathUtils.Abs(offsetY) + MathUtils.Abs(offsetZ);
                var weightDivisor = distance <= 0 ? 1 : 4 * distance * distance + 2;
                var weight = 1f / weightDivisor;
                heaterTemperatureSum += weight * 36f * heat;
                heaterWeightSum += weight;
            }

            else if (block.IsHeatBlocker(cellValueFast))
            {
                var distance = MathUtils.Abs(offsetX) + MathUtils.Abs(offsetY) + MathUtils.Abs(offsetZ);
                var weightDivisor = distance <= 0 ? 1 : 4 * distance * distance + 2;
                var weight = 1f / weightDivisor;
                float seasonTemperature = terrain.SeasonTemperature;
                float altitudeAdjustment = SubsystemWeather.GetTemperatureAdjustmentAtHeight(y2);
                var blockTemperature = block is WaterBlock
                    ? MathUtils.Max(chunkAtCell.GetTemperatureFast(x2, z2) + seasonTemperature - 6f, 0f) +
                      altitudeAdjustment
                    : !(block is IceBlock)
                        ? chunkAtCell.GetTemperatureFast(x2, z2) + seasonTemperature + altitudeAdjustment
                        : 0f + seasonTemperature + altitudeAdjustment;
                blockTemperatureSum += weight * blockTemperature;
                blockWeightSum += weight;
            }

            else if (y >= chunkAtCell.GetTopHeightFast(x2, z2))
            {
                var distance = MathUtils.Abs(offsetX) + MathUtils.Abs(offsetY) + MathUtils.Abs(offsetZ);
                var weightDivisor = distance <= 0 ? 1 : 4 * distance * distance + 2;
                var weight = 1f / weightDivisor;
                var precipitationShaftInfo = _subsystemWeather.GetPrecipitationShaftInfo(x, z);
                float seasonTemperature = terrain.SeasonTemperature;
                var precipitationAdjustment = y >= precipitationShaftInfo.YLimit
                    ? MathUtils.Lerp(0f, -2f, precipitationShaftInfo.Intensity)
                    : 0f;
                var daylightAdjustment = MathUtils.Lerp(-6f, 0f, _subsystemSky.SkyLightIntensity);
                float altitudeAdjustment = SubsystemWeather.GetTemperatureAdjustmentAtHeight(y2);
                outdoorTemperatureSum += weight *
                                         (chunkAtCell.GetTemperatureFast(x2, z2) +
                                          seasonTemperature +
                                          precipitationAdjustment +
                                          daylightAdjustment +
                                          altitudeAdjustment);
                outdoorWeightSum += weight;
            }

            else if (_toVisit.Count < 4090)
            {
                if (offsetX > -30)
                {
                    _toVisit.Add(packedOffset - 1);
                }

                if (offsetX < 30)
                {
                    _toVisit.Add(packedOffset + 1);
                }

                if (offsetY > -30)
                {
                    _toVisit.Add(packedOffset - 64);
                }

                if (offsetY < 30)
                {
                    _toVisit.Add(packedOffset + 64);
                }

                if (offsetZ > -30)
                {
                    _toVisit.Add(packedOffset - 4096);
                }

                if (offsetZ < 30)
                {
                    _toVisit.Add(packedOffset + 4096);
                }
            }
        }

        // 只累计半径七格内、到温度计的射线未被不透明碰撞方块遮挡的热源。
        var visibleHeat = 0f;
        for (var k = -7; k <= 7; k++)
        {
            for (var l = -7; l <= 7; l++)
            {
                var chunkAtCell2 = SubsystemTerrain.Terrain.GetChunkAtCell(x + k, z + l, false);
                if (chunkAtCell2 is not { HasContents: true })
                {
                    continue;
                }

                for (var m = -7; m <= 7; m++)
                {
                    var distanceSquared = k * k + m * m + l * l;
                    if (distanceSquared is > 49 or <= 0)
                    {
                        continue;
                    }

                    var x3 = (x + k) & 0xF;
                    var sourceY = y + m;
                    var z3 = (z + l) & 0xF;
                    if (sourceY is < 0 or >= 256)
                    {
                        continue;
                    }

                    var heat2 = GetHeat(chunkAtCell2.GetCellValueFast(x3, sourceY, z3));
                    if (heat2 > 0f &&
                        !SubsystemTerrain.Raycast(
                            new Vector3(x, y, z) + new Vector3(0.5f, 0.75f, 0.5f),
                            new Vector3(x + k, y + m, z + l) + new Vector3(0.5f, 0.75f, 0.5f),
                            false,
                            true,
                            delegate(int raycastValue, float _)
                            {
                                var block2 = WorldContentBinding.For(Project).Catalog
                                    .Blocks[Terrain.ExtractContents(raycastValue)];
                                return block2 is { Collidable: true, Transparent: false };
                            }
                        ).HasValue)
                    {
                        visibleHeat += heat2 * 3f / (distanceSquared + 2);
                    }
                }
            }
        }

        var temperatureSum = 0f;
        var totalWeight = 0f;

        if (visibleHeat > 0f)
        {
            var visibleHeatWeight = 3f * visibleHeat;
            temperatureSum += 35f * visibleHeatWeight;
            totalWeight += visibleHeatWeight;
        }

        if (blockWeightSum > 0f)
        {
            var blockWeight = 1f;
            temperatureSum += blockTemperatureSum / blockWeightSum * blockWeight;
            totalWeight += blockWeight;
        }

        if (outdoorWeightSum > 0f)
        {
            var outdoorWeight = 4f * MathUtils.Pow(outdoorWeightSum, 0.25f);
            temperatureSum += outdoorTemperatureSum / outdoorWeightSum * outdoorWeight;
            totalWeight += outdoorWeight;
        }

        if (heaterWeightSum > 0f)
        {
            var heaterWeight = 1.5f * MathUtils.Pow(heaterWeightSum, 0.25f);
            temperatureSum += heaterTemperatureSum / heaterWeightSum * heaterWeight;
            totalWeight += heaterWeight;
        }

        if (meterInsulation > 0f)
        {
            temperatureSum += meterTemperature * meterInsulation;
            totalWeight += meterInsulation;
        }

        temperature = totalWeight > 0f ? temperatureSum / totalWeight : meterTemperature;
        temperatureFlux = totalWeight - meterInsulation;
    }

    private float GetHeat(int value)
    {
        var contents = Terrain.ExtractContents(value);
        return WorldContentBinding.For(Project).Catalog.Blocks[contents].GetHeat(value);
    }

    private void SimulateThermometer(int x, int y, int z, bool invalidateTerrainOnChange)
    {
        var key = new Point3(x, y, z);
        if (!_thermometersByPoint.TryGetValue(key, out var previousReading))
        {
            return;
        }

        CalculateTemperature(x, y, z, 0f, 0f, out var temperature, out _);
        var newReading = MathUtils.Clamp((int)MathUtils.Round(temperature), 0, 15);
        if (newReading == previousReading)
        {
            return;
        }

        _thermometersByPoint[new Point3(x, y, z)] = newReading;
        if (!invalidateTerrainOnChange)
        {
            return;
        }

        var chunkAtCell = SubsystemTerrain.Terrain.GetChunkAtCell(x, z, false);
        if (chunkAtCell != null)
        {
            SubsystemTerrain.InvalidateMesh(chunkAtCell.Coords, true);
        }
    }

    private void AddMeter(int value, int x, int y, int z)
    {
        if (Terrain.ExtractContents(value) != 120)
        {
            return;
        }

        _thermometersByPoint.Add(new Point3(x, y, z), 0);
        SimulateThermometer(x, y, z, false);
    }

    private void RemoveMeter(int value, int x, int y, int z)
    {
        _thermometersByPoint.Remove(new Point3(x, y, z));
    }
}
