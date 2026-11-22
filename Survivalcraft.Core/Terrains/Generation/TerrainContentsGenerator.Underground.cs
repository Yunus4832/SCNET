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
                var sourceChunkX = i + chunk.Coords.X;
                var sourceChunkZ = j + chunk.Coords.Y;
                var random = new Random(seed + sourceChunkX + 71 * sourceChunkZ);
                var skippedDraws = random.Int(0, 10);
                for (var k = 0; k < skippedDraws; k++)
                {
                    random.Int(0, 1);
                }

                var mountainFactor = CalculateMountainRangeFactor(sourceChunkX * 16, sourceChunkZ * 16);
                for (var l = 0; l < 5; l++)
                {
                    var x = sourceChunkX * 16 + random.Int(0, 15);
                    var y = random.Int(50, 150);
                    var z = sourceChunkZ * 16 + random.Int(0, 15);
                    _dirtPocketBrushes[random.Int(0, _dirtPocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, x, y, z, 3);
                }

                for (var m = 0; m < 30; m++)
                {
                    var worldX = sourceChunkX * 16 + random.Int(0, 15);
                    var worldY = random.Int(20, 160);
                    var worldZ = sourceChunkZ * 16 + random.Int(0, 15);
                    _gravelPocketBrushes[random.Int(0, _gravelPocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, worldX, worldY, worldZ, 3);
                }

                for (var n = 0; n < 5; n++)
                {
                    var worldX = sourceChunkX * 16 + random.Int(0, 15);
                    var worldY = random.Int(10, 200);
                    var worldZ = sourceChunkZ * 16 + random.Int(0, 15);
                    _limestonePocketBrushes[random.Int(0, _limestonePocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, worldX, worldY, worldZ, 3);
                }

                for (var pocket = 0; pocket < 1; pocket++)
                {
                    var worldX = sourceChunkX * 16 + random.Int(0, 15);
                    var worldY = random.Int(50, 70);
                    var worldZ = sourceChunkZ * 16 + random.Int(0, 15);
                    _clayPocketBrushes[random.Int(0, _clayPocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, worldX, worldY, worldZ, 3);
                }

                for (var pocket = 0; pocket < 30; pocket++)
                {
                    var worldX = sourceChunkX * 16 + random.Int(0, 15);
                    var worldY = random.Int(20, 160);
                    var worldZ = sourceChunkZ * 16 + random.Int(0, 15);
                    _sandPocketBrushes[random.Int(0, _sandPocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, worldX, worldY, worldZ, 4);
                }

                for (var pocket = 0; pocket < 4; pocket++)
                {
                    var worldX = sourceChunkX * 16 + random.Int(0, 15);
                    var worldY = random.Int(40, 60);
                    var worldZ = sourceChunkZ * 16 + random.Int(0, 15);
                    _basaltPocketBrushes[random.Int(0, _basaltPocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, worldX, worldY, worldZ, 4);
                }

                for (var pocket = 0; pocket < 3; pocket++)
                {
                    var worldX = sourceChunkX * 16 + random.Int(0, 15);
                    var worldY = random.Int(20, 40);
                    var worldZ = sourceChunkZ * 16 + random.Int(0, 15);
                    _basaltPocketBrushes[random.Int(0, _basaltPocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, worldX, worldY, worldZ, 3);
                }

                for (var pocket = 0; pocket < 6; pocket++)
                {
                    var worldX = sourceChunkX * 16 + random.Int(0, 15);
                    var worldY = random.Int(4, 50);
                    var worldZ = sourceChunkZ * 16 + random.Int(0, 15);
                    _granitePocketBrushes[random.Int(0, _granitePocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, worldX, worldY, worldZ, 67);
                }

                for (var pocket = 0; pocket < 30; pocket++)
                {
                    var worldX = sourceChunkX * 16 + random.Int(0, 15);
                    var worldY = random.Int(4, 180);
                    var worldZ = sourceChunkZ * 16 + random.Int(0, 15);
                    _gravelPocketBrushes[random.Int(0, _gravelPocketBrushes.Count - 1)]
                        .PaintFastSelective(chunk, worldX, worldY, worldZ, 67);
                }

                if (random.Bool(0.02f + 0.01f * mountainFactor))
                {
                    var waterOriginX = sourceChunkX * 16;
                    var waterLevel = random.Int(40, 60);
                    var waterOriginZ = sourceChunkZ * 16;
                    var waterPocketCount = random.Int(1, 3);
                    for (var waterPocket = 0; waterPocket < waterPocketCount; waterPocket++)
                    {
                        var waterOffset = random.Vector2(7f);
                        var waterLocalX = 8 + (int)MathUtils.Round(waterOffset.X);
                        var waterOffsetY = 0;
                        var waterLocalZ = 8 + (int)MathUtils.Round(waterOffset.Y);
                        _waterPocketBrushes[random.Int(0, _waterPocketBrushes.Count - 1)]
                            .PaintFast(
                                chunk,
                                waterOriginX + waterLocalX,
                                waterLevel + waterOffsetY,
                                waterOriginZ + waterLocalZ
                            );
                    }
                }

                if (!random.Bool(0.06f + 0.05f * mountainFactor))
                {
                    continue;
                }

                var magmaOriginX = sourceChunkX * 16;
                var magmaLevel = random.Int(15, 20); //岩浆池往下调整
                var magmaOriginZ = sourceChunkZ * 16;
                var magmaPocketCount = random.Int(1, 2);
                for (var magmaPocket = 0; magmaPocket < magmaPocketCount; magmaPocket++)
                {
                    var magmaOffset = random.Vector2(7f);
                    var magmaLocalX = 8 + (int)MathUtils.Round(magmaOffset.X);
                    var magmaOffsetY = random.Int(0, 1);
                    var magmaLocalZ = 8 + (int)MathUtils.Round(magmaOffset.Y);
                    _magmaPocketBrushes[random.Int(0, _magmaPocketBrushes.Count - 1)]
                        .PaintFast(
                            chunk,
                            magmaOriginX + magmaLocalX,
                            magmaLevel + magmaOffsetY,
                            magmaOriginZ + magmaLocalZ
                        );
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

        var branches = new List<CavePoint>();
        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        for (var i = x - 2; i <= x + 2; i++)
        {
            for (var j = y - 2; j <= y + 2; j++)
            {
                branches.Clear();
                var random = new Random(seed + i + 9973 * j);
                var entranceX = i * 16 + random.Int(0, 15);
                var entranceZ = j * 16 + random.Int(0, 15);
                var probability = 0.5f;
                if (!random.Bool(probability))
                {
                    continue;
                }

                var entranceHeight = (int)CalculateHeight(entranceX, entranceZ);
                var heightAtPositiveX = (int)CalculateHeight(entranceX + 3, entranceZ);
                var heightAtPositiveZ = (int)CalculateHeight(entranceX, entranceZ + 3);
                var position = new Vector3(entranceX, entranceHeight - 1, entranceZ);
                var surfaceTangentX = new Vector3(3f, heightAtPositiveX - entranceHeight, 0f);
                var surfaceTangentZ = new Vector3(0f, heightAtPositiveZ - entranceHeight, 3f);
                var direction = Vector3.Normalize(Vector3.Cross(surfaceTangentX, surfaceTangentZ));
                if (direction.Y > -0.6f)
                {
                    branches.Add(
                        new CavePoint
                        {
                            Position = position,
                            Direction = direction,
                            BrushType = 0,
                            Length = random.Int(80, 240)
                        }
                    );
                }

                var centerX = i * 16 + 8;
                var centerZ = j * 16 + 8;
                var branchIndex = 0;
                while (branchIndex < branches.Count)
                {
                    var cavePoint = branches[branchIndex];
                    var brushes = _caveBrushesByType[cavePoint.BrushType];
                    brushes[random.Int(0, brushes.Count - 1)].PaintFastAvoidWater(
                        chunk,
                        Terrain.ToCell(cavePoint.Position.X),
                        Terrain.ToCell(cavePoint.Position.Y),
                        Terrain.ToCell(cavePoint.Position.Z)
                    );
                    cavePoint.Position += 2f * cavePoint.Direction;
                    cavePoint.StepsTaken += 2;
                    var offsetFromCenterX = cavePoint.Position.X - centerX;
                    var offsetFromCenterZ = cavePoint.Position.Z - centerZ;
                    if (random.Bool(0.5f))
                    {
                        var turnOffset = Vector3.Normalize(random.Vector3(1f));
                        if ((offsetFromCenterX < -25.5f && turnOffset.X < 0f) ||
                            (offsetFromCenterX > 25.5f && turnOffset.X > 0f))
                        {
                            turnOffset.X = 0f - turnOffset.X;
                        }

                        if ((offsetFromCenterZ < -25.5f && turnOffset.Z < 0f) ||
                            (offsetFromCenterZ > 25.5f && turnOffset.Z > 0f))
                        {
                            turnOffset.Z = 0f - turnOffset.Z;
                        }

                        if ((cavePoint.Direction.Y < -0.5f && turnOffset.Y < -10f) ||
                            (cavePoint.Direction.Y > 0.1f && turnOffset.Y > 0f))
                        {
                            turnOffset.Y = 0f - turnOffset.Y;
                        }

                        cavePoint.Direction = Vector3.Normalize(cavePoint.Direction + 0.5f * turnOffset);
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
                        branches.Count < 12 &&
                        cavePoint.StepsTaken > 20 &&
                        cavePoint.Position.Y < 58f)
                    {
                        branches.Add(
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
                        MathUtils.Abs(offsetFromCenterX) > 34f ||
                        MathUtils.Abs(offsetFromCenterZ) > 34f ||
                        cavePoint.Position.Y < 5f ||
                        cavePoint.Position.Y > 246f)
                    {
                        branchIndex++;
                    }
                    else if (cavePoint.StepsTaken % 20 == 0)
                    {
                        var surfaceHeight = CalculateHeight(cavePoint.Position.X, cavePoint.Position.Z);
                        if (cavePoint.Position.Y > surfaceHeight + 1f)
                        {
                            branchIndex++;
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
