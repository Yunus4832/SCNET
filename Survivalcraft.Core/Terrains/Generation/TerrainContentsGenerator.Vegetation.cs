namespace Game.Terrains.Generation;

public partial class TerrainContentsGenerator
{
    public void GenerateGrassAndPlants(TerrainChunk chunk)
    {
        if (!tgExtras)
        {
            return;
        }

        var random = new Random(seed + chunk.Coords.X + 3943 * chunk.Coords.Y);
        for (var i = 0; i < 16; i++)
        {
            for (var j = 0; j < 16; j++)
            {
                for (var num = 254; num >= 0; num--)
                {
                    var cellValueFast = chunk.GetCellValueFast(i, num, j);
                    var num2 = Terrain.ExtractContents(cellValueFast);
                    if (num2 == 0)
                    {
                        continue;
                    }

                    if (!(_subsystemTerrain.Catalog.Blocks[num2] is FluidBlock))
                    {
                        var temperatureFast = chunk.GetTemperatureFast(i, j);
                        var humidityFast = chunk.GetHumidityFast(i, j);
                        var num3 = PlantsManager.GenerateRandomPlantValue(
                            random,
                            cellValueFast,
                            temperatureFast,
                            humidityFast,
                            num + 1
                        );
                        if (num3 != 0)
                        {
                            chunk.SetCellValueFast(i, num + 1, j, num3);
                        }

                        if (num2 == 2)
                        {
                            chunk.SetCellValueFast(i, num, j, Terrain.MakeBlockValue(8, 0));
                        }
                    }

                    break;
                }
            }
        }
    }

    public void GenerateBottomSuckers(TerrainChunk chunk)
    {
        if (!tgExtras)
        {
            return;
        }

        var random = new Random(seed + chunk.Coords.X + 2210 * chunk.Coords.Y);
        for (var i = 0; i < 16; i++)
        {
            for (var j = 0; j < 16; j++)
            {
                if (!random.Bool(0.2f))
                {
                    continue;
                }

                var num = chunk.Origin.X + i;
                var num2 = chunk.Origin.Y + j;
                var temperatureFast = chunk.GetTemperatureFast(i, j);
                if (CalculateOceanShoreDistance(num, num2) > 10f)
                {
                    continue;
                }

                var num3 = 0;
                for (var num4 = 254; num4 >= 0; num4--)
                {
                    if (Terrain.ExtractContents(chunk.GetCellValueFast(i, num4, j)) == 18)
                    {
                        num3++;
                        var face = random.Int(0, 5);
                        var point = CellFace.FaceToPoint3(face);
                        if (i + point.X < 0 ||
                            i + point.X >= 16 ||
                            num4 + point.Y < 0 ||
                            num4 + point.Y >= 254 ||
                            j + point.Z < 0 ||
                            j + point.Z >= 16)
                        {
                            continue;
                        }

                        var cellValueFast = chunk.GetCellValueFast(i + point.X, num4 + point.Y, j + point.Z);
                        if (!_subsystemBottomSuckerBlockBehavior.IsSupport(cellValueFast, CellFace.OppositeFace(face)))
                        {
                            continue;
                        }

                        var num5 = 0;
                        var num6 = 0.6f;
                        var num7 = 0.4f;
                        if (temperatureFast < 8)
                        {
                            num6 = 0.9f;
                            num7 = 0.1f;
                        }

                        if (num3 > 6)
                        {
                            num6 *= 0.25f;
                        }

                        if (num3 > 12)
                        {
                            num7 *= 0.5f;
                        }

                        if (num3 < 4)
                        {
                            num7 *= 0.5f;
                        }

                        if (num4 < 45)
                        {
                            num6 *= 0.1f;
                            num7 *= 0.1f;
                        }

                        var num8 = random.Float(0f, 1f);
                        num8 -= num6;
                        if (num5 == 0 && num8 < 0f)
                        {
                            num5 = 226;
                        }

                        num8 -= num7;
                        if (num5 == 0 && num8 < 0f)
                        {
                            num5 = 229;
                        }

                        if (num5 == 0)
                        {
                            continue;
                        }

                        var face2 = random.Int(0, 3);
                        var data = BottomSuckerBlock.SetFace(
                            BottomSuckerBlock.SetSubvariant(0, face2),
                            CellFace.OppositeFace(face)
                        );
                        var value = Terrain.MakeBlockValue(num5, data);
                        chunk.SetCellValueFast(i, num4, j, value);
                    }
                    else
                    {
                        num3 = 0;
                    }
                }
            }
        }
    }

    public void GenerateCacti(TerrainChunk chunk)
    {
        if (!tgExtras)
        {
            return;
        }

        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        var random = new Random(seed + x + 1991 * y);
        if (!random.Bool(0.5f))
        {
            return;
        }

        var num = random.Int(0, MathUtils.Max(1, 1));
        for (var i = 0; i < num; i++)
        {
            var num2 = random.Int(3, 12);
            var num3 = random.Int(3, 12);
            var humidityFast = chunk.GetHumidityFast(num2, num3);
            var temperatureFast = chunk.GetTemperatureFast(num2, num3);
            if (humidityFast >= 6 || temperatureFast <= 8)
            {
                continue;
            }

            for (var j = 0; j < 8; j++)
            {
                var num4 = num2 + random.Int(-2, 2);
                var num5 = num3 + random.Int(-2, 2);
                for (var num6 = 251; num6 >= 0; num6--)
                {
                    switch (Terrain.ExtractContents(chunk.GetCellValueFast(num4, num6, num5)))
                    {
                        case 7:
                        {
                            for (var k = num6 + 1;
                                 k <= num6 + 3 &&
                                 chunk.GetCellContentsFast(num4 + 1, k, num5) == 0 &&
                                 chunk.GetCellContentsFast(num4 - 1, k, num5) == 0 &&
                                 chunk.GetCellContentsFast(num4, k, num5 + 1) == 0 &&
                                 chunk.GetCellContentsFast(num4, k, num5 - 1) == 0;
                                 k++)
                            {
                                chunk.SetCellValueFast(num4, k, num5, Terrain.MakeBlockValue(127));
                            }

                            break;
                        }
                        case 0:
                            continue;
                    }

                    break;
                }
            }
        }
    }

    public void GeneratePumpkins(TerrainChunk chunk)
    {
        if (!tgExtras)
        {
            return;
        }

        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        var random = new Random(seed + x + 1495 * y);
        if (!random.Bool(0.2f))
        {
            return;
        }

        var num = random.Int(0, MathUtils.Max(1, 1));
        for (var i = 0; i < num; i++)
        {
            var num2 = random.Int(1, 14);
            var num3 = random.Int(1, 14);
            var humidityFast = chunk.GetHumidityFast(num2, num3);
            var temperatureFast = chunk.GetTemperatureFast(num2, num3);
            if (humidityFast < 10 || temperatureFast <= 6)
            {
                continue;
            }

            for (var j = 0; j < 5; j++)
            {
                var x2 = num2 + random.Int(-1, 1);
                var z = num3 + random.Int(-1, 1);
                for (var num4 = 254; num4 >= 0; num4--)
                {
                    switch (Terrain.ExtractContents(chunk.GetCellValueFast(x2, num4, z)))
                    {
                        case 8:
                            chunk.SetCellValueFast(
                                x2,
                                num4 + 1,
                                z,
                                random.Bool(0.25f) ? Terrain.MakeBlockValue(244) : Terrain.MakeBlockValue(131)
                            );
                            break;
                        case 0:
                            continue;
                    }

                    break;
                }
            }
        }
    }

    public void GenerateKelp(TerrainChunk chunk)
    {
        if (!tgExtras)
        {
            return;
        }

        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        var random = new Random(0);
        var num = 0f;
        for (var i = 0; i < 9; i++)
        {
            var num2 = i % 3 - 1;
            var num3 = i / 3 - 1;
            random.Seed(seed + x + num2 + 850 * (y + num3));
            if (!random.Bool(0.2f))
            {
                continue;
            }

            num = MathUtils.Max(num, 0.025f);
            if (i == 4)
            {
                num = MathUtils.Max(num, 0.1f);
            }
        }

        if (num == 0f)
        {
            return;
        }

        random.Seed(seed + x + 850 * y);
        var num4 = random.Int(0, MathUtils.Max((int)(256f * num), 1));
        for (var j = 0; j < num4; j++)
        {
            var num5 = random.Int(2, 13);
            var num6 = random.Int(2, 13);
            var num7 = num5 + chunk.Origin.X;
            var num8 = num6 + chunk.Origin.Y;
            var num9 = random.Int(10, 26);
            var num10 = 6;
            var flag = true;
            if (CalculateOceanShoreDistance(num7, num8) > 5f)
            {
                num10 = 4;
                flag = false;
            }

            if (num9 <= 0)
            {
                continue;
            }

            for (var k = 0; k < num9; k++)
            {
                var x2 = num5 + random.Int(-2, 2);
                var z = num6 + random.Int(-2, 2);
                var num11 = 0;
                for (var num12 = 254; num12 >= 0; num12--)
                {
                    var num13 = Terrain.ExtractContents(chunk.GetCellValueFast(x2, num12, z));
                    var block = _subsystemTerrain.Catalog.Blocks[num13];
                    if (num13 == 0)
                    {
                        continue;
                    }

                    if (block is not WaterBlock)
                    {
                        if (num13 is 2 or 7 or 72 && num11 >= 2)
                        {
                            var num14 = flag ? random.Int(num11 - 2, num11 - 1) : random.Int(num11 - 1, num11);
                            for (var l = 0; l < num14; l++)
                            {
                                chunk.SetCellValueFast(x2, num12 + 1 + l, z, Terrain.MakeBlockValue(232));
                            }
                        }

                        break;
                    }

                    num11++;
                    if (num11 > num10)
                    {
                        break;
                    }
                }
            }
        }
    }

    public void GenerateSeagrass(TerrainChunk chunk)
    {
        if (!tgExtras)
        {
            return;
        }

        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        var random = new Random(seed + x + 378 * y);
        for (var i = 0; i < 6; i++)
        {
            var num = random.Int(1, 14);
            var num2 = random.Int(1, 14);
            var num3 = chunk.Origin.X + num;
            var num4 = chunk.Origin.Y + num2;
            var flag = CalculateOceanShoreDistance(num3, num4) < 10f;
            var num5 = random.Int(1, 3);
            for (var j = 0; j < num5; j++)
            {
                var x2 = num + random.Int(-1, 1);
                var z = num2 + random.Int(-1, 1);
                var num6 = 0;
                for (var num7 = 254; num7 >= 0; num7--)
                {
                    var num8 = Terrain.ExtractContents(chunk.GetCellValueFast(x2, num7, z));
                    switch (num8)
                    {
                        case 18:
                            num6++;
                            if (num6 <= 16)
                            {
                                continue;
                            }

                            break;
                        default:
                            if (num6 > 1 && num8 is 2 or 7 or 72 or 3)
                            {
                                var x3 = !random.Bool(0.1f) ? 1 : 2;
                                x3 = flag ? MathUtils.Min(x3, num6 - 1) : MathUtils.Min(x3, num6);
                                for (var k = 0; k < x3; k++)
                                {
                                    chunk.SetCellValueFast(x2, num7 + 1 + k, z, Terrain.MakeBlockValue(233));
                                }
                            }

                            break;
                        case 0:
                            continue;
                    }

                    break;
                }
            }
        }
    }

    public void GenerateIvy(TerrainChunk chunk)
    {
        if (!tgExtras)
        {
            return;
        }

        var random = new Random(seed + chunk.Coords.X + 2191 * chunk.Coords.Y);
        var num = random.Int(0, MathUtils.Max(12, 1));
        for (var i = 0; i < num; i++)
        {
            var num2 = random.Int(4, 11);
            var num3 = random.Int(4, 11);
            var humidityFast = chunk.GetHumidityFast(num2, num3);
            var temperatureFast = chunk.GetTemperatureFast(num2, num3);
            if (humidityFast < 10 || temperatureFast < 10)
            {
                continue;
            }

            var num4 = chunk.CalculateTopmostCellHeight(num2, num3);
            for (var j = 0; j < 100; j++)
            {
                var num5 = num2 + random.Int(-3, 3);
                var num6 = MathUtils.Clamp(num4 + random.Int(-12, 1), 1, 255);
                var num7 = num3 + random.Int(-3, 3);
                switch (Terrain.ExtractContents(chunk.GetCellValueFast(num5, num6, num7)))
                {
                    case 2:
                    case 3:
                    case 8:
                    case 9:
                    case 12:
                    case 66:
                    case 67:
                    {
                        var num8 = random.Int(0, 3);
                        for (var k = 0; k < 4; k++)
                        {
                            var face = (k + num8) % 4;
                            var point = CellFace.FaceToPoint3(face);
                            if (chunk.GetCellContentsFast(num5 + point.X, num6, num7 + point.Z) != 0)
                            {
                                continue;
                            }

                            var num9 = num6 - 1;
                            while (num9 >= 1 &&
                                   chunk.GetCellContentsFast(num5 + point.X, num9, num7 + point.Z) == 0 &&
                                   chunk.GetCellContentsFast(num5, num9, num7) != 0)
                            {
                                num9--;
                            }

                            if (chunk.GetCellContentsFast(num5 + point.X, num9, num7 + point.Z) != 0)
                            {
                                break;
                            }

                            num9++;
                            var value = Terrain.MakeBlockValue(
                                197,
                                IvyBlock.SetFace(0, CellFace.OppositeFace(face))
                            );
                            while (num9 >= 1 && chunk.GetCellContentsFast(num5 + point.X, num9, num7 + point.Z) == 0)
                            {
                                chunk.SetCellValueFast(num5 + point.X, num9, num7 + point.Z, value);
                                if (IvyBlock.IsGrowthStopCell(num5 + point.X, num9, num7 + point.Z))
                                {
                                    break;
                                }

                                num9--;
                            }

                            break;
                        }

                        break;
                    }
                }
            }
        }
    }
}
