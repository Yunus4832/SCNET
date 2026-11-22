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
                for (var surfaceY = 254; surfaceY >= 0; surfaceY--)
                {
                    var cellValueFast = chunk.GetCellValueFast(i, surfaceY, j);
                    var groundContents = Terrain.ExtractContents(cellValueFast);
                    if (groundContents == 0)
                    {
                        continue;
                    }

                    if (!(_subsystemTerrain.Catalog.Blocks[groundContents] is FluidBlock))
                    {
                        var temperatureFast = chunk.GetTemperatureFast(i, j);
                        var humidityFast = chunk.GetHumidityFast(i, j);
                        var plantValue = PlantsManager.GenerateRandomPlantValue(
                            random,
                            cellValueFast,
                            temperatureFast,
                            humidityFast,
                            surfaceY + 1
                        );
                        if (plantValue != 0)
                        {
                            chunk.SetCellValueFast(i, surfaceY + 1, j, plantValue);
                        }

                        if (groundContents == 2)
                        {
                            chunk.SetCellValueFast(i, surfaceY, j, Terrain.MakeBlockValue(8, 0));
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

                var worldX = chunk.Origin.X + i;
                var worldZ = chunk.Origin.Y + j;
                var temperatureFast = chunk.GetTemperatureFast(i, j);
                if (CalculateOceanShoreDistance(worldX, worldZ) > 10f)
                {
                    continue;
                }

                var waterDepth = 0;
                for (var cellY = 254; cellY >= 0; cellY--)
                {
                    if (Terrain.ExtractContents(chunk.GetCellValueFast(i, cellY, j)) == 18)
                    {
                        waterDepth++;
                        var face = random.Int(0, 5);
                        var point = CellFace.FaceToPoint3(face);
                        if (i + point.X < 0 ||
                            i + point.X >= 16 ||
                            cellY + point.Y < 0 ||
                            cellY + point.Y >= 254 ||
                            j + point.Z < 0 ||
                            j + point.Z >= 16)
                        {
                            continue;
                        }

                        var cellValueFast = chunk.GetCellValueFast(i + point.X, cellY + point.Y, j + point.Z);
                        if (!_subsystemBottomSuckerBlockBehavior.IsSupport(cellValueFast, CellFace.OppositeFace(face)))
                        {
                            continue;
                        }

                        var creatureContents = 0;
                        var seaUrchinChance = 0.6f;
                        var starfishChance = 0.4f;
                        if (temperatureFast < 8)
                        {
                            seaUrchinChance = 0.9f;
                            starfishChance = 0.1f;
                        }

                        if (waterDepth > 6)
                        {
                            seaUrchinChance *= 0.25f;
                        }

                        if (waterDepth > 12)
                        {
                            starfishChance *= 0.5f;
                        }

                        if (waterDepth < 4)
                        {
                            starfishChance *= 0.5f;
                        }

                        if (cellY < 45)
                        {
                            seaUrchinChance *= 0.1f;
                            starfishChance *= 0.1f;
                        }

                        var remainingRoll = random.Float(0f, 1f);
                        remainingRoll -= seaUrchinChance;
                        if (creatureContents == 0 && remainingRoll < 0f)
                        {
                            creatureContents = 226;
                        }

                        remainingRoll -= starfishChance;
                        if (creatureContents == 0 && remainingRoll < 0f)
                        {
                            creatureContents = 229;
                        }

                        if (creatureContents == 0)
                        {
                            continue;
                        }

                        var face2 = random.Int(0, 3);
                        var data = BottomSuckerBlock.SetFace(
                            BottomSuckerBlock.SetSubvariant(0, face2),
                            CellFace.OppositeFace(face)
                        );
                        var value = Terrain.MakeBlockValue(creatureContents, data);
                        chunk.SetCellValueFast(i, cellY, j, value);
                    }
                    else
                    {
                        waterDepth = 0;
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

        var patchCount = random.Int(0, MathUtils.Max(1, 1));
        for (var i = 0; i < patchCount; i++)
        {
            var centerX = random.Int(3, 12);
            var centerZ = random.Int(3, 12);
            var humidityFast = chunk.GetHumidityFast(centerX, centerZ);
            var temperatureFast = chunk.GetTemperatureFast(centerX, centerZ);
            if (humidityFast >= 6 || temperatureFast <= 8)
            {
                continue;
            }

            for (var j = 0; j < 8; j++)
            {
                var localX = centerX + random.Int(-2, 2);
                var localZ = centerZ + random.Int(-2, 2);
                for (var groundY = 251; groundY >= 0; groundY--)
                {
                    switch (Terrain.ExtractContents(chunk.GetCellValueFast(localX, groundY, localZ)))
                    {
                        case 7:
                        {
                            for (var k = groundY + 1;
                                 k <= groundY + 3 &&
                                 chunk.GetCellContentsFast(localX + 1, k, localZ) == 0 &&
                                 chunk.GetCellContentsFast(localX - 1, k, localZ) == 0 &&
                                 chunk.GetCellContentsFast(localX, k, localZ + 1) == 0 &&
                                 chunk.GetCellContentsFast(localX, k, localZ - 1) == 0;
                                 k++)
                            {
                                chunk.SetCellValueFast(localX, k, localZ, Terrain.MakeBlockValue(127));
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

        var patchCount = random.Int(0, MathUtils.Max(1, 1));
        for (var i = 0; i < patchCount; i++)
        {
            var centerX = random.Int(1, 14);
            var centerZ = random.Int(1, 14);
            var humidityFast = chunk.GetHumidityFast(centerX, centerZ);
            var temperatureFast = chunk.GetTemperatureFast(centerX, centerZ);
            if (humidityFast < 10 || temperatureFast <= 6)
            {
                continue;
            }

            for (var j = 0; j < 5; j++)
            {
                var x2 = centerX + random.Int(-1, 1);
                var z = centerZ + random.Int(-1, 1);
                for (var groundY = 254; groundY >= 0; groundY--)
                {
                    switch (Terrain.ExtractContents(chunk.GetCellValueFast(x2, groundY, z)))
                    {
                        case 8:
                            chunk.SetCellValueFast(
                                x2,
                                groundY + 1,
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
        var patchDensity = 0f;
        for (var i = 0; i < 9; i++)
        {
            var neighborOffsetX = i % 3 - 1;
            var neighborOffsetZ = i / 3 - 1;
            random.Seed(seed + x + neighborOffsetX + 850 * (y + neighborOffsetZ));
            if (!random.Bool(0.2f))
            {
                continue;
            }

            patchDensity = MathUtils.Max(patchDensity, 0.025f);
            if (i == 4)
            {
                patchDensity = MathUtils.Max(patchDensity, 0.1f);
            }
        }

        if (patchDensity == 0f)
        {
            return;
        }

        random.Seed(seed + x + 850 * y);
        var patchCount = random.Int(0, MathUtils.Max((int)(256f * patchDensity), 1));
        for (var j = 0; j < patchCount; j++)
        {
            var centerX = random.Int(2, 13);
            var centerZ = random.Int(2, 13);
            var worldX = centerX + chunk.Origin.X;
            var worldZ = centerZ + chunk.Origin.Y;
            var plantAttempts = random.Int(10, 26);
            var maxWaterDepth = 6;
            var nearShore = true;
            if (CalculateOceanShoreDistance(worldX, worldZ) > 5f)
            {
                maxWaterDepth = 4;
                nearShore = false;
            }

            if (plantAttempts <= 0)
            {
                continue;
            }

            for (var k = 0; k < plantAttempts; k++)
            {
                var localX = centerX + random.Int(-2, 2);
                var localZ = centerZ + random.Int(-2, 2);
                var waterDepth = 0;
                for (var cellY = 254; cellY >= 0; cellY--)
                {
                    var contents = Terrain.ExtractContents(chunk.GetCellValueFast(localX, cellY, localZ));
                    var block = _subsystemTerrain.Catalog.Blocks[contents];
                    if (contents == 0)
                    {
                        continue;
                    }

                    if (block is not WaterBlock)
                    {
                        if (contents is 2 or 7 or 72 && waterDepth >= 2)
                        {
                            var kelpHeight = nearShore
                                ? random.Int(waterDepth - 2, waterDepth - 1)
                                : random.Int(waterDepth - 1, waterDepth);
                            for (var l = 0; l < kelpHeight; l++)
                            {
                                chunk.SetCellValueFast(localX, cellY + 1 + l, localZ, Terrain.MakeBlockValue(232));
                            }
                        }

                        break;
                    }

                    waterDepth++;
                    if (waterDepth > maxWaterDepth)
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
            var centerX = random.Int(1, 14);
            var centerZ = random.Int(1, 14);
            var worldX = chunk.Origin.X + centerX;
            var worldZ = chunk.Origin.Y + centerZ;
            var nearShore = CalculateOceanShoreDistance(worldX, worldZ) < 10f;
            var plantAttempts = random.Int(1, 3);
            for (var j = 0; j < plantAttempts; j++)
            {
                var localX = centerX + random.Int(-1, 1);
                var localZ = centerZ + random.Int(-1, 1);
                var waterDepth = 0;
                for (var cellY = 254; cellY >= 0; cellY--)
                {
                    var contents = Terrain.ExtractContents(chunk.GetCellValueFast(localX, cellY, localZ));
                    switch (contents)
                    {
                        case 18:
                            waterDepth++;
                            if (waterDepth <= 16)
                            {
                                continue;
                            }

                            break;
                        default:
                            if (waterDepth > 1 && contents is 2 or 7 or 72 or 3)
                            {
                                var plantHeight = !random.Bool(0.1f) ? 1 : 2;
                                plantHeight = nearShore
                                    ? MathUtils.Min(plantHeight, waterDepth - 1)
                                    : MathUtils.Min(plantHeight, waterDepth);
                                for (var k = 0; k < plantHeight; k++)
                                {
                                    chunk.SetCellValueFast(localX, cellY + 1 + k, localZ, Terrain.MakeBlockValue(233));
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
        var patchCount = random.Int(0, MathUtils.Max(12, 1));
        for (var i = 0; i < patchCount; i++)
        {
            var centerX = random.Int(4, 11);
            var centerZ = random.Int(4, 11);
            var humidityFast = chunk.GetHumidityFast(centerX, centerZ);
            var temperatureFast = chunk.GetTemperatureFast(centerX, centerZ);
            if (humidityFast < 10 || temperatureFast < 10)
            {
                continue;
            }

            var topY = chunk.CalculateTopmostCellHeight(centerX, centerZ);
            for (var j = 0; j < 100; j++)
            {
                var supportX = centerX + random.Int(-3, 3);
                var supportY = MathUtils.Clamp(topY + random.Int(-12, 1), 1, 255);
                var supportZ = centerZ + random.Int(-3, 3);
                switch (Terrain.ExtractContents(chunk.GetCellValueFast(supportX, supportY, supportZ)))
                {
                    case 2:
                    case 3:
                    case 8:
                    case 9:
                    case 12:
                    case 66:
                    case 67:
                    {
                        var firstFace = random.Int(0, 3);
                        for (var k = 0; k < 4; k++)
                        {
                            var face = (k + firstFace) % 4;
                            var point = CellFace.FaceToPoint3(face);
                            if (chunk.GetCellContentsFast(supportX + point.X, supportY, supportZ + point.Z) != 0)
                            {
                                continue;
                            }

                            var ivyY = supportY - 1;
                            while (ivyY >= 1 &&
                                   chunk.GetCellContentsFast(supportX + point.X, ivyY, supportZ + point.Z) == 0 &&
                                   chunk.GetCellContentsFast(supportX, ivyY, supportZ) != 0)
                            {
                                ivyY--;
                            }

                            if (chunk.GetCellContentsFast(supportX + point.X, ivyY, supportZ + point.Z) != 0)
                            {
                                break;
                            }

                            ivyY++;
                            var value = Terrain.MakeBlockValue(
                                197,
                                IvyBlock.SetFace(0, CellFace.OppositeFace(face))
                            );
                            while (ivyY >= 1 &&
                                   chunk.GetCellContentsFast(supportX + point.X, ivyY, supportZ + point.Z) == 0)
                            {
                                chunk.SetCellValueFast(supportX + point.X, ivyY, supportZ + point.Z, value);
                                if (IvyBlock.IsGrowthStopCell(supportX + point.X, ivyY, supportZ + point.Z))
                                {
                                    break;
                                }

                                ivyY--;
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
