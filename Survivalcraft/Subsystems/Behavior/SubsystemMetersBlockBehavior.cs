using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemMetersBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
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

    public override int[] HandledBlocks => [120, 121];

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
        if (BlocksManager.Blocks[cellContents].Transparent)
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
            if (key.X >= chunk.Origin.X && key.X < chunk.Origin.X + 16 && key.Z >= chunk.Origin.Y &&
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
    public void CalculateTemperature(int x, int y, int z, float meterTemperature, float meterInsulation,
        out float temperature, out float temperatureFlux)
    {
        _toVisit.Clear();
        for (var i = 0; i < _visited.Length; i++)
        {
            _visited[i] = 0;
        }
        //            _toVisit 是一个动态数组，用于存储需要访问的坐标。
        //_visited 是一个布尔数组，用于标记哪些坐标已经访问过，避免重复计算。

        var num = 0f;
        var num2 = 0f; //num 和 num2: 累积热阻块的温度和权重。
        var num3 = 0f;
        var num4 = 0f; //num3 和 num4: 累积外部环境（天空）的温度和权重。
        var num5 = 0f;
        var num6 = 0f; //num5 和 num6: 累积热源块的温度和权重。
        //            这些变量用于累积不同来源的温度和权重：


        _toVisit.Add(133152);

        //            133152 是一个编码形式的起始坐标(0, 0, 0)，表示相对于温度计的偏移量。编码规则如下：
        //x 偏移量占最低 6 位。
        //y 偏移量占中间 6 位。
        //z 偏移量占最高 6 位。
        //例如：x = 0, y = 0, z = 0，编码为 0x000000。
        for (var j = 0; j < _toVisit.Count; j++)
        {
            var num7 = _toVisit.Array[j];
            if ((_visited[num7 / 32] & (1 << num7)) != 0)
            {
                continue;
            }

            _visited[num7 / 32] |= 1 << num7;
            //                遍历 _toVisit 数组，取出当前偏移量 num7。
            //检查当前偏移量是否已经访问过。如果访问过，则跳过。
            //标记当前偏移量为已访问。

            var num8 = (num7 & 0x3F) - 32;
            var num9 = ((num7 >> 6) & 0x3F) - 32;
            var num10 = ((num7 >> 12) & 0x3F) - 32;
            //通过位运算解码 num7，分别计算 x, y, z 的偏移量，并减去 32 以恢复原始值。
            var num11 = num8 + x;
            var num12 = num9 + y;
            var num13 = num10 + z;
            // 将偏移量加到温度计的基准坐标(x, y, z) 上，得到当前需要检查的绝对坐标(num11, num12, num13)。

            var terrain = SubsystemTerrain.Terrain;
            var chunkAtCell = terrain.GetChunkAtCell(num11, num13, false);
            if (chunkAtCell == null || num12 < 0 || num12 >= 256)
            // 获取当前坐标所在的地形块。如果地形块不存在（如超出世界范围）或 y 坐标超出有效范围[0, 256)，则跳过。
            {
                continue;
            }

            var x2 = num11 & 0xF;
            var y2 = num12;
            var z2 = num13 & 0xF;
            var cellValueFast = chunkAtCell.GetCellValueFast(x2, y2, z2);
            var num14 = Terrain.ExtractContents(cellValueFast);
            var block = BlocksManager.Blocks[num14];
            //                计算当前坐标在地形块中的局部坐标(x2, y2, z2)。
            //获取该坐标处的块值 cellValueFast。
            //提取块的内容 ID num14，并获取对应的块对象 block。

            var heat = GetHeat(cellValueFast);
            if (heat > 0f)
            {
                var num15 = MathUtils.Abs(num8) + MathUtils.Abs(num9) + MathUtils.Abs(num10);
                var num16 = num15 <= 0 ? 1 : 4 * num15 * num15 + 2;
                var num17 = 1f / num16;
                num5 += num17 * 36f * heat;
                num6 += num17;
            }
            //                如果当前块是热源块（heat > 0），计算其对温度的贡献。
            //根据与温度计的曼哈顿距离 num15，计算权重 num17。
            //累积热源块的温度贡献到 num5 和权重到 num6。

            else if (block.IsHeatBlocker(cellValueFast))
            {
                var num18 = MathUtils.Abs(num8) + MathUtils.Abs(num9) + MathUtils.Abs(num10);
                var num19 = num18 <= 0 ? 1 : 4 * num18 * num18 + 2;
                var num20 = 1f / num19;
                float num21 = terrain.SeasonTemperature;
                float num22 = SubsystemWeather.GetTemperatureAdjustmentAtHeight(y2);
                var num23 = block is WaterBlock
                    ? MathUtils.Max(chunkAtCell.GetTemperatureFast(x2, z2) + num21 - 6f, 0f) + num22
                    : !(block is IceBlock)
                        ? chunkAtCell.GetTemperatureFast(x2, z2) + num21 + num22
                        : 0f + num21 + num22;
                num += num20 * num23;
                num2 += num20;
            }
            //                如果当前块是热阻块，计算其对温度的贡献。
            //考虑季节温度(num21)、高度调整温度(num22) 和块类型（如水块、冰块）的特殊处理。

            else if (y >= chunkAtCell.GetTopHeightFast(x2, z2))
            {
                //外部环境
                var num24 = MathUtils.Abs(num8) + MathUtils.Abs(num9) + MathUtils.Abs(num10);
                var num25 = num24 <= 0 ? 1 : 4 * num24 * num24 + 2;
                var num26 = 1f / num25;
                var precipitationShaftInfo = _subsystemWeather.GetPrecipitationShaftInfo(x, z);
                float num27 = terrain.SeasonTemperature;
                var num28 = y >= precipitationShaftInfo.YLimit
                    ? MathUtils.Lerp(0f, -2f, precipitationShaftInfo.Intensity)
                    : 0f;
                var num29 = MathUtils.Lerp(-6f, 0f, _subsystemSky.SkyLightIntensity);
                float num30 = SubsystemWeather.GetTemperatureAdjustmentAtHeight(y2);
                num3 += num26 * (chunkAtCell.GetTemperatureFast(x2, z2) + num27 + num28 + num29 + num30);
                num4 += num26;
            }
            //                如果当前坐标位于地形顶部以上，计算外部环境的温度贡献。
            //考虑季节温度、降水强度、光照强度等因素。

            else if (_toVisit.Count < 4090)
            {
                if (num8 > -30)
                {
                    _toVisit.Add(num7 - 1);
                }

                if (num8 < 30)
                {
                    _toVisit.Add(num7 + 1);
                }

                if (num9 > -30)
                {
                    _toVisit.Add(num7 - 64);
                }

                if (num9 < 30)
                {
                    _toVisit.Add(num7 + 64);
                }

                if (num10 > -30)
                {
                    _toVisit.Add(num7 - 4096);
                }

                if (num10 < 30)
                {
                    _toVisit.Add(num7 + 4096);
                }
            }
            // 如果当前坐标不是热源块、热阻块或外部环境，并且 _toVisit 未超出容量限制，向队列中添加邻居坐标。
        }

        var num31 = 0f;
        //局部热源处理
        for (var k = -7; k <= 7; k++)
        {
            for (var l = -7; l <= 7; l++)
            {
                var chunkAtCell2 = SubsystemTerrain.Terrain.GetChunkAtCell(x + k, z + l, false);
                if (chunkAtCell2 == null || chunkAtCell2.MainThreadState < TerrainChunkState.InvalidVertices1)
                {
                    continue;
                }

                for (var m = -7; m <= 7; m++)
                {
                    var num32 = k * k + m * m + l * l;
                    if (num32 is > 49 or <= 0)
                    {
                        continue;
                    }

                    var x3 = (x + k) & 0xF;
                    var num33 = y + m;
                    var z3 = (z + l) & 0xF;
                    if (num33 is < 0 or >= 256)
                    {
                        continue;
                    }

                    var heat2 = GetHeat(chunkAtCell2.GetCellValueFast(x3, num33, z3));
                    if (heat2 > 0f && !SubsystemTerrain.Raycast(new Vector3(x, y, z) + new Vector3(0.5f, 0.75f, 0.5f),
                            new Vector3(x + k, y + m, z + l) + new Vector3(0.5f, 0.75f, 0.5f), false, true,
                            delegate (int raycastValue, float _)
                            {
                                var block2 = BlocksManager.Blocks[Terrain.ExtractContents(raycastValue)];
                                return block2 is { Collidable: true, Transparent: false };
                            }).HasValue)
                    {
                        num31 += heat2 * 3f / (num32 + 2);
                    }
                }
            }
        }

        // 计算局部范围内热源块的贡献，范围为以温度计为中心的 7 格立方体。
        // 使用射线检测（Raycast）确保热源块对温度计的影响没有被障碍物遮挡。
        var num34 = 0f;
        var num35 = 0f;

        if (num31 > 0f)
        {
            var num36 = 3f * num31;
            num34 += 35f * num36;
            num35 += num36;
        }

        if (num2 > 0f)
        {
            var num37 = 1f;
            num34 += num / num2 * num37;
            num35 += num37;
        }

        if (num4 > 0f)
        {
            var num38 = 4f * MathUtils.Pow(num4, 0.25f);
            num34 += num3 / num4 * num38;
            num35 += num38;
        }

        if (num6 > 0f)
        {
            var num39 = 1.5f * MathUtils.Pow(num6, 0.25f);
            num34 += num5 / num6 * num39;
            num35 += num39;
        }

        if (meterInsulation > 0f)
        {
            num34 += meterTemperature * meterInsulation;
            num35 += meterInsulation;
        }

        // 根据累积的温度和权重，计算加权平均温度 num34 和总权重 num35。
        // 对不同来源的温度（局部热源、热阻块、外部环境等）赋予不同的权重。
        temperature = num35 > 0f ? num34 / num35 : meterTemperature;
        // 如果总权重 num35 > 0，计算最终温度为加权平均值；否则，使用温度计本身的温度。
        // 计算温度波动值 temperatureFlux，表示总权重减去绝缘系数。
        temperatureFlux = num35 - meterInsulation;
    }

    private static float GetHeat(int value)
    {
        var num = Terrain.ExtractContents(value);
        return BlocksManager.Blocks[num].GetHeat(value);
    }

    private void SimulateThermometer(int x, int y, int z, bool invalidateTerrainOnChange)
    {
        var key = new Point3(x, y, z);
        if (!_thermometersByPoint.TryGetValue(key, out var num))
        {
            return;
        }

        CalculateTemperature(x, y, z, 0f, 0f, out var temperature, out _);
        var num2 = MathUtils.Clamp((int)MathUtils.Round(temperature), 0, 15);
        if (num2 == num)
        {
            return;
        }

        _thermometersByPoint[new Point3(x, y, z)] = num2;
        if (!invalidateTerrainOnChange)
        {
            return;
        }

        var chunkAtCell = SubsystemTerrain.Terrain.GetChunkAtCell(x, z, false);
        if (chunkAtCell != null)
        {
            SubsystemTerrain.TerrainUpdater.DowngradeChunkNeighborhoodState(chunkAtCell.Coords, 0,
                TerrainChunkState.InvalidVertices1, true);
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
