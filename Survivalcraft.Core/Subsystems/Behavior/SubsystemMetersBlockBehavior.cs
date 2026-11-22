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

    //        x, y, z: 温度计所在的坐标。
    //meterTemperature: 温度计本身的温度。
    //meterInsulation: 温度计的绝缘系数。
    //temperature: 计算出的温度（通过 out 返回）。
    //temperatureFlux: 温度波动值（通过 out 返回）。
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
        //            _toVisit 是一个动态数组，用于存储需要访问的坐标。
        //_visited 是一个布尔数组，用于标记哪些坐标已经访问过，避免重复计算。

        var blockTemperatureSum = 0f;
        var blockWeightSum = 0f; //blockTemperatureSum 和 blockWeightSum: 累积热阻块的温度和权重。
        var outdoorTemperatureSum = 0f;
        var outdoorWeightSum = 0f; //outdoorTemperatureSum 和 outdoorWeightSum: 累积外部环境（天空）的温度和权重。
        var heaterTemperatureSum = 0f;
        var heaterWeightSum = 0f; //heaterTemperatureSum 和 heaterWeightSum: 累积热源块的温度和权重。
        //            这些变量用于累积不同来源的温度和权重：


        _toVisit.Add(133152);

        //            133152 是一个编码形式的起始坐标(0, 0, 0)，表示相对于温度计的偏移量。编码规则如下：
        //x 偏移量占最低 6 位。
        //y 偏移量占中间 6 位。
        //z 偏移量占最高 6 位。
        //例如：x = 0, y = 0, z = 0，编码为 0x000000。
        for (var j = 0; j < _toVisit.Count; j++)
        {
            var packedOffset = _toVisit.Array[j];
            if ((_visited[packedOffset / 32] & (1 << packedOffset)) != 0)
            {
                continue;
            }

            _visited[packedOffset / 32] |= 1 << packedOffset;
            //                遍历 _toVisit 数组，取出当前偏移量 packedOffset。
            //检查当前偏移量是否已经访问过。如果访问过，则跳过。
            //标记当前偏移量为已访问。

            var offsetX = (packedOffset & 0x3F) - 32;
            var offsetY = ((packedOffset >> 6) & 0x3F) - 32;
            var offsetZ = ((packedOffset >> 12) & 0x3F) - 32;
            //通过位运算解码 packedOffset，分别计算 x, y, z 的偏移量，并减去 32 以恢复原始值。
            var worldX = offsetX + x;
            var worldY = offsetY + y;
            var worldZ = offsetZ + z;
            // 将偏移量加到温度计的基准坐标(x, y, z) 上，得到当前需要检查的绝对坐标(worldX, worldY, worldZ)。

            var terrain = SubsystemTerrain.Terrain;
            var chunkAtCell = terrain.GetChunkAtCell(worldX, worldZ, false);
            if (chunkAtCell == null || worldY < 0 || worldY >= 256)
                // 获取当前坐标所在的地形块。如果地形块不存在（如超出世界范围）或 y 坐标超出有效范围[0, 256)，则跳过。
            {
                continue;
            }

            var x2 = worldX & 0xF;
            var y2 = worldY;
            var z2 = worldZ & 0xF;
            var cellValueFast = chunkAtCell.GetCellValueFast(x2, y2, z2);
            var contents = Terrain.ExtractContents(cellValueFast);
            var block = WorldContentBinding.For(Project).Catalog.Blocks[contents];
            //                计算当前坐标在地形块中的局部坐标(x2, y2, z2)。
            //获取该坐标处的块值 cellValueFast。
            //提取块的内容 ID contents，并获取对应的块对象 block。

            var heat = GetHeat(cellValueFast);
            if (heat > 0f)
            {
                var distance = MathUtils.Abs(offsetX) + MathUtils.Abs(offsetY) + MathUtils.Abs(offsetZ);
                var weightDivisor = distance <= 0 ? 1 : 4 * distance * distance + 2;
                var weight = 1f / weightDivisor;
                heaterTemperatureSum += weight * 36f * heat;
                heaterWeightSum += weight;
            }
            //                如果当前块是热源块（heat > 0），计算其对温度的贡献。
            //根据与温度计的曼哈顿距离 distance，计算权重 weight。
            //累积热源块的温度贡献到 heaterTemperatureSum 和权重到 heaterWeightSum。

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
            //                如果当前块是热阻块，计算其对温度的贡献。
            //考虑季节温度(seasonTemperature)、高度调整温度(altitudeAdjustment) 和块类型（如水块、冰块）的特殊处理。

            else if (y >= chunkAtCell.GetTopHeightFast(x2, z2))
            {
                //外部环境
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
            //                如果当前坐标位于地形顶部以上，计算外部环境的温度贡献。
            //考虑季节温度、降水强度、光照强度等因素。

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
            // 如果当前坐标不是热源块、热阻块或外部环境，并且 _toVisit 未超出容量限制，向队列中添加邻居坐标。
        }

        var visibleHeat = 0f;
        //局部热源处理
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

        // 计算局部范围内热源块的贡献，范围为以温度计为中心的 7 格立方体。
        // 使用射线检测（Raycast）确保热源块对温度计的影响没有被障碍物遮挡。
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

        // 根据累积的温度和权重，计算加权平均温度 temperatureSum 和总权重 totalWeight。
        // 对不同来源的温度（局部热源、热阻块、外部环境等）赋予不同的权重。
        temperature = totalWeight > 0f ? temperatureSum / totalWeight : meterTemperature;
        // 如果总权重 totalWeight > 0，计算最终温度为加权平均值；否则，使用温度计本身的温度。
        // 计算温度波动值 temperatureFlux，表示总权重减去绝缘系数。
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
