namespace Game.Terrains.Generation;

public partial class TerrainContentsGenerator
{
    public void GenerateMinerals(TerrainChunk chunk)
    {
        if (!tgCavesAndPockets)
        {
            return;
        }

        var chunkX = chunk.Coords.X;
        var chunkZ = chunk.Coords.Y;
        for (var sourceChunkX = chunkX - 1; sourceChunkX <= chunkX + 1; sourceChunkX++)
        {
            for (var sourceChunkZ = chunkZ - 1; sourceChunkZ <= chunkZ + 1; sourceChunkZ++)
            {
                var random = new Random(seed + sourceChunkX + 119 * sourceChunkZ);
                var skippedDraws = random.Int(0, 10);
                for (var draw = 0; draw < skippedDraws; draw++)
                {
                    random.Int(0, 1);
                }

                var mountainFactor = CalculateMountainRangeFactor(sourceChunkX * 16, sourceChunkZ * 16);
                var coalVeins = (int)(5f +
                                      3f *
                                      mountainFactor *
                                      SimplexNoise.OctavedNoise(sourceChunkX, sourceChunkZ, 0.33f, 1, 1f, 1f));
                for (var vein = 0; vein < coalVeins; vein++)
                {
                    var worldX = sourceChunkX * 16 + random.Int(0, 15);
                    var worldY = random.Int(5, 200);
                    var worldZ = sourceChunkZ * 16 + random.Int(0, 15);
                    _coalBrushes[random.Int(0, _coalBrushes.Count - 1)]
                        .PaintFastSelective(chunk, worldX, worldY, worldZ, 3);
                }

                var copperVeins = (int)(6f +
                                        2f *
                                        mountainFactor *
                                        SimplexNoise.OctavedNoise(
                                            sourceChunkX + 1211,
                                            sourceChunkZ + 396,
                                            0.33f,
                                            1,
                                            1f,
                                            1f
                                        ));
                for (var vein = 0; vein < copperVeins; vein++)
                {
                    var worldX = sourceChunkX * 16 + random.Int(0, 15);
                    var worldY = random.Int(20, 65);
                    var worldZ = sourceChunkZ * 16 + random.Int(0, 15);
                    _copperBrushes[random.Int(0, _copperBrushes.Count - 1)]
                        .PaintFastSelective(chunk, worldX, worldY, worldZ, 3);
                }

                var ironVeins = (int)(5f +
                                      2f *
                                      mountainFactor *
                                      SimplexNoise.OctavedNoise(
                                          sourceChunkX + 713,
                                          sourceChunkZ + 211,
                                          0.33f,
                                          1,
                                          1f,
                                          1f
                                      ));
                for (var vein = 0; vein < ironVeins; vein++)
                {
                    var worldX = sourceChunkX * 16 + random.Int(0, 15);
                    var worldY = random.Int(2, 40);
                    var worldZ = sourceChunkZ * 16 + random.Int(0, 15);
                    _ironBrushes[random.Int(0, _ironBrushes.Count - 1)]
                        .PaintFastSelective(chunk, worldX, worldY, worldZ, 67);
                }

                var saltpeterVeins = (int)(3f +
                                           3f *
                                           mountainFactor *
                                           SimplexNoise.OctavedNoise(
                                               sourceChunkX + 915,
                                               sourceChunkZ + 272,
                                               0.33f,
                                               1,
                                               1f,
                                               1f
                                           ));
                for (var vein = 0; vein < saltpeterVeins; vein++)
                {
                    var worldX = sourceChunkX * 16 + random.Int(0, 15);
                    var worldY = random.Int(50, 90);
                    var worldZ = sourceChunkZ * 16 + random.Int(0, 15);
                    _saltpeterBrushes[random.Int(0, _saltpeterBrushes.Count - 1)]
                        .PaintFastSelective(chunk, worldX, worldY, worldZ, 4);
                }

                var sulphurVeins = (int)(3f +
                                         2f *
                                         mountainFactor *
                                         SimplexNoise.OctavedNoise(
                                             sourceChunkX + 711,
                                             sourceChunkZ + 1194,
                                             0.33f,
                                             1,
                                             1f,
                                             1f
                                         ));
                for (var vein = 0; vein < sulphurVeins; vein++)
                {
                    var worldX = sourceChunkX * 16 + random.Int(0, 15);
                    var worldY = random.Int(2, 40);
                    var worldZ = sourceChunkZ * 16 + random.Int(0, 15);
                    _sulphurBrushes[random.Int(0, _sulphurBrushes.Count - 1)]
                        .PaintFastSelective(chunk, worldX, worldY, worldZ, 67);
                }

                var diamondVeins = (int)(0.5f +
                                         2f *
                                         mountainFactor *
                                         SimplexNoise.OctavedNoise(
                                             sourceChunkX + 432,
                                             sourceChunkZ + 907,
                                             0.33f,
                                             1,
                                             1f,
                                             1f
                                         ));
                for (var vein = 0; vein < diamondVeins; vein++)
                {
                    var worldX = sourceChunkX * 16 + random.Int(0, 15);
                    var worldY = random.Int(2, 15);
                    var worldZ = sourceChunkZ * 16 + random.Int(0, 15);
                    _diamondBrushes[random.Int(0, _diamondBrushes.Count - 1)]
                        .PaintFastSelective(chunk, worldX, worldY, worldZ, 67);
                }

                var germaniumVeins = (int)(3f +
                                           2f *
                                           mountainFactor *
                                           SimplexNoise.OctavedNoise(
                                               sourceChunkX + 799,
                                               sourceChunkZ + 131,
                                               0.33f,
                                               1,
                                               1f,
                                               1f
                                           ));
                for (var vein = 0; vein < germaniumVeins; vein++)
                {
                    var worldX = sourceChunkX * 16 + random.Int(0, 15);
                    var worldY = random.Int(2, 50);
                    var worldZ = sourceChunkZ * 16 + random.Int(0, 15);
                    _germaniumBrushes[random.Int(0, _germaniumBrushes.Count - 1)]
                        .PaintFastSelective(chunk, worldX, worldY, worldZ, 67);
                }
            }
        }
    }

    public void GeneratePockets(TerrainChunk chunk)
    {
        if (!tgCavesAndPockets)
        {
            return;
        }

        for (var i = -1; i <= 1; i++)
        {
            for (var j = -1; j <= 1; j++)
            {
                var num = i + chunk.Coords.X;
                var num2 = j + chunk.Coords.Y;
                var random = new Random(seed + num + 71 * num2);
                var num3 = random.Int(0, 10);
                for (var k = 0; k < num3; k++)
                {
                    random.Int(0, 1);
                }

                var num4 = CalculateMountainRangeFactor(num * 16, num2 * 16);
                for (var l = 0; l < 5; l++)
                {
                    var x = num * 16 + random.Int(0, 15);
                    var y = random.Int(50, 150);
                    var z = num2 * 16 + random.Int(0, 15);
                    _dirtPocketBrushes[random.Int(0, _dirtPocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, x, y, z, 3);
                }

                for (var m = 0; m < 30; m++)
                {
                    var x2 = num * 16 + random.Int(0, 15);
                    var y2 = random.Int(20, 160);
                    var z2 = num2 * 16 + random.Int(0, 15);
                    _gravelPocketBrushes[random.Int(0, _gravelPocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, x2, y2, z2, 3);
                }

                for (var n = 0; n < 5; n++)
                {
                    var x3 = num * 16 + random.Int(0, 15);
                    var y3 = random.Int(10, 200);
                    var z3 = num2 * 16 + random.Int(0, 15);
                    _limestonePocketBrushes[random.Int(0, _limestonePocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, x3, y3, z3, 3);
                }

                for (var num5 = 0; num5 < 1; num5++)
                {
                    var x4 = num * 16 + random.Int(0, 15);
                    var y4 = random.Int(50, 70);
                    var z4 = num2 * 16 + random.Int(0, 15);
                    _clayPocketBrushes[random.Int(0, _clayPocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, x4, y4, z4, 3);
                }

                for (var num6 = 0; num6 < 30; num6++)
                {
                    var x5 = num * 16 + random.Int(0, 15);
                    var y5 = random.Int(20, 160);
                    var z5 = num2 * 16 + random.Int(0, 15);
                    _sandPocketBrushes[random.Int(0, _sandPocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, x5, y5, z5, 4);
                }

                for (var num7 = 0; num7 < 4; num7++)
                {
                    var x6 = num * 16 + random.Int(0, 15);
                    var y6 = random.Int(40, 60);
                    var z6 = num2 * 16 + random.Int(0, 15);
                    _basaltPocketBrushes[random.Int(0, _basaltPocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, x6, y6, z6, 4);
                }

                for (var num8 = 0; num8 < 3; num8++)
                {
                    var x7 = num * 16 + random.Int(0, 15);
                    var y7 = random.Int(20, 40);
                    var z7 = num2 * 16 + random.Int(0, 15);
                    _basaltPocketBrushes[random.Int(0, _basaltPocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, x7, y7, z7, 3);
                }

                for (var num9 = 0; num9 < 6; num9++)
                {
                    var x8 = num * 16 + random.Int(0, 15);
                    var y8 = random.Int(4, 50);
                    var z8 = num2 * 16 + random.Int(0, 15);
                    _granitePocketBrushes[random.Int(0, _granitePocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, x8, y8, z8, 67);
                }

                for (var num10 = 0; num10 < 30; num10++)
                {
                    var x9 = num * 16 + random.Int(0, 15);
                    var y9 = random.Int(4, 180);
                    var z9 = num2 * 16 + random.Int(0, 15);
                    _gravelPocketBrushes[random.Int(0, _gravelPocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, x9, y9, z9, 67);
                }

                if (random.Bool(0.02f + 0.01f * num4))
                {
                    var num11 = num * 16;
                    var num12 = random.Int(40, 60);
                    var num13 = num2 * 16;
                    var num14 = random.Int(1, 3);
                    for (var num15 = 0; num15 < num14; num15++)
                    {
                        var vector = random.Vector2(7f);
                        var num16 = 8 + (int)MathUtils.Round(vector.X);
                        var num17 = 0;
                        var num18 = 8 + (int)MathUtils.Round(vector.Y);
                        _waterPocketBrushes[random.Int(0, _waterPocketBrushes.Count - 1)]
                            .PaintFast(chunk, num11 + num16, num12 + num17, num13 + num18);
                    }
                }

                if (!random.Bool(0.06f + 0.05f * num4))
                {
                    continue;
                }

                var num19 = num * 16;
                var num20 = random.Int(15, 20); //岩浆池往下调整
                var num21 = num2 * 16;
                var num22 = random.Int(1, 2);
                for (var num23 = 0; num23 < num22; num23++)
                {
                    var vector2 = random.Vector2(7f);
                    var num24 = 8 + (int)MathUtils.Round(vector2.X);
                    var num25 = random.Int(0, 1);
                    var num26 = 8 + (int)MathUtils.Round(vector2.Y);
                    _magmaPocketBrushes[random.Int(0, _magmaPocketBrushes.Count - 1)]
                        .PaintFast(chunk, num19 + num24, num20 + num25, num21 + num26);
                }
            }
        }
    }

    public void GenerateCaves(TerrainChunk chunk)
    {
        if (!tgCavesAndPockets)
        {
            return;
        }

        var list = new List<CavePoint>();
        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        for (var i = x - 2; i <= x + 2; i++)
        {
            for (var j = y - 2; j <= y + 2; j++)
            {
                list.Clear();
                var random = new Random(seed + i + 9973 * j);
                var num = i * 16 + random.Int(0, 15);
                var num2 = j * 16 + random.Int(0, 15);
                var probability = 0.5f;
                if (!random.Bool(probability))
                {
                    continue;
                }

                var num3 = (int)CalculateHeight(num, num2);
                var num4 = (int)CalculateHeight(num + 3, num2);
                var num5 = (int)CalculateHeight(num, num2 + 3);
                var position = new Vector3(num, num3 - 1, num2);
                var v = new Vector3(3f, num4 - num3, 0f);
                var v2 = new Vector3(0f, num5 - num3, 3f);
                var direction = Vector3.Normalize(Vector3.Cross(v, v2));
                if (direction.Y > -0.6f)
                {
                    list.Add(
                        new CavePoint
                        {
                            Position = position,
                            Direction = direction,
                            BrushType = 0,
                            Length = random.Int(80, 240)
                        }
                    );
                }

                var num6 = i * 16 + 8;
                var num7 = j * 16 + 8;
                var num8 = 0;
                while (num8 < list.Count)
                {
                    var cavePoint = list[num8];
                    var list2 = _caveBrushesByType[cavePoint.BrushType];
                    list2[random.Int(0, list2.Count - 1)].PaintFastAvoidWater(
                        chunk,
                        Terrain.ToCell(cavePoint.Position.X),
                        Terrain.ToCell(cavePoint.Position.Y),
                        Terrain.ToCell(cavePoint.Position.Z)
                    );
                    cavePoint.Position += 2f * cavePoint.Direction;
                    cavePoint.StepsTaken += 2;
                    var num9 = cavePoint.Position.X - num6;
                    var num10 = cavePoint.Position.Z - num7;
                    if (random.Bool(0.5f))
                    {
                        var vector = Vector3.Normalize(random.Vector3(1f));
                        if ((num9 < -25.5f && vector.X < 0f) || (num9 > 25.5f && vector.X > 0f))
                        {
                            vector.X = 0f - vector.X;
                        }

                        if ((num10 < -25.5f && vector.Z < 0f) || (num10 > 25.5f && vector.Z > 0f))
                        {
                            vector.Z = 0f - vector.Z;
                        }

                        if ((cavePoint.Direction.Y < -0.5f && vector.Y < -10f) ||
                            (cavePoint.Direction.Y > 0.1f && vector.Y > 0f))
                        {
                            vector.Y = 0f - vector.Y;
                        }

                        cavePoint.Direction = Vector3.Normalize(cavePoint.Direction + 0.5f * vector);
                    }

                    if (cavePoint.StepsTaken > 20 && random.Bool(0.06f))
                    {
                        cavePoint.Direction = Vector3.Normalize(random.Vector3(1f) * new Vector3(1f, 0.33f, 1f));
                    }

                    if (cavePoint.StepsTaken > 20 && random.Bool(0.05f))
                    {
                        cavePoint.Direction.Y = 0f;
                        cavePoint.BrushType = MathUtils.Min(cavePoint.BrushType + 2, _caveBrushesByType.Count - 1);
                    }

                    if (cavePoint.StepsTaken > 30 && random.Bool(0.03f))
                    {
                        cavePoint.Direction.X = 0f;
                        cavePoint.Direction.Y = -1f;
                        cavePoint.Direction.Z = 0f;
                    }

                    if (cavePoint is { StepsTaken: > 30, Position.Y: < 30f } && random.Bool(0.02f))
                    {
                        cavePoint.Direction.X = 0f;
                        cavePoint.Direction.Y = 1f;
                        cavePoint.Direction.Z = 0f;
                    }

                    if (random.Bool(0.33f))
                    {
                        cavePoint.BrushType =
                            (int)(MathUtils.Pow(random.Float(0f, 0.999f), 7f) * _caveBrushesByType.Count);
                    }

                    if (random.Bool(0.06f) &&
                        list.Count < 12 &&
                        cavePoint.StepsTaken > 20 &&
                        cavePoint.Position.Y < 58f)
                    {
                        list.Add(
                            new CavePoint
                            {
                                Position = cavePoint.Position,
                                Direction = Vector3.Normalize(random.Vector3(1f, 1f) * new Vector3(1f, 0.33f, 1f)),
                                BrushType =
                                    (int)(MathUtils.Pow(random.Float(0f, 0.999f), 7f) * _caveBrushesByType.Count),
                                Length = random.Int(40, 180)
                            }
                        );
                    }

                    if (cavePoint.StepsTaken >= cavePoint.Length ||
                        MathUtils.Abs(num9) > 34f ||
                        MathUtils.Abs(num10) > 34f ||
                        cavePoint.Position.Y < 5f ||
                        cavePoint.Position.Y > 246f)
                    {
                        num8++;
                    }
                    else if (cavePoint.StepsTaken % 20 == 0)
                    {
                        var num11 = CalculateHeight(cavePoint.Position.X, cavePoint.Position.Z);
                        if (cavePoint.Position.Y > num11 + 1f)
                        {
                            num8++;
                        }
                    }
                }
            }
        }
    }

    public class CavePoint
    {
        public int BrushType;

        public Vector3 Direction;

        public int Length;

        public Vector3 Position;

        public int StepsTaken;
    }
}
