namespace Game.Managers;

public static class PlantsManager
{
    private static readonly List<TerrainBrush>[] _treeBrushesByType;

    private static readonly int[] _treeTrunksByType;

    private static readonly int[] _treeLeavesByType;

    static PlantsManager()
    {
        _treeBrushesByType = new List<TerrainBrush>[EnumUtils.GetEnumValues(typeof(TreeType)).Max() + 1];
        _treeTrunksByType =
        [
            9,
            10,
            11,
            11,
            255,
            262
        ];
        _treeLeavesByType =
        [
            Terrain.MakeBlockValue(12, DeciduousLeavesBlock.SetSeason(0, Season.Spring)),
            Terrain.MakeBlockValue(13, DeciduousLeavesBlock.SetSeason(0, Season.Spring)),
            14,
            225,
            Terrain.MakeBlockValue(256, DeciduousLeavesBlock.SetSeason(0, Season.Spring)),
            Terrain.MakeBlockValue(263, DeciduousLeavesBlock.SetSeason(0, Season.Spring))
        ];
        var random = new Random(33);
        _treeBrushesByType[0] = [];
        for (var i = 0; i < 16; i++)
        {
            var array = new[]
            {
                5, 6, 7, 8, 9, 10, 11, 11, 12, 12, 13, 13, 14, 15, 16, 18
            };
            var height4 = array[i];
            var branchesCount = (int)MathUtils.Lerp(10f, 22f, i / 16f);
            var item = CreateTreeBrush(
                random,
                GetTreeTrunkValue(TreeType.Oak),
                GetTreeLeavesValue(TreeType.Oak),
                height4,
                branchesCount,
                3,
                delegate(int y, int _)
                {
                    var leafChance = 0.4f;
                    if (y < 0.2f * height4)
                    {
                        leafChance = 0f;
                    }
                    else if (y >= 0.2f * height4 && y <= height4)
                    {
                        leafChance *= 1.5f;
                    }

                    return leafChance;
                },
                delegate(int y)
                {
                    if (y < height4 * 0.3f || y > height4 * 0.9f)
                    {
                        return 0f;
                    }

                    var maxBranchLength = y < height4 * 0.7f ? 0.5f * height4 : 0.35f * height4;
                    return random.Float(0.33f, 1f) * maxBranchLength;
                }
            );
            _treeBrushesByType[0].Add(item);
        }

        _treeBrushesByType[1] = [];
        for (var j = 0; j < 16; j++)
        {
            var array2 = new[]
            {
                4, 5, 6, 7, 7, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 12
            };
            var height3 = array2[j];
            var branchesCount2 = (int)MathUtils.Lerp(0f, 20f, j / 16f);
            var item2 = CreateTreeBrush(
                random,
                GetTreeTrunkValue(TreeType.Birch),
                GetTreeLeavesValue(TreeType.Birch),
                height3,
                branchesCount2,
                3,
                delegate(int y, int _)
                {
                    var leafChance = 0.66f;
                    if (y < height3 / 2 - 1)
                    {
                        leafChance = 0f;
                    }
                    else if (y > height3 / 2 && y <= height3)
                    {
                        leafChance *= 1.5f;
                    }

                    return leafChance;
                },
                y => y < height3 * 0.35f || y > height3 * 0.75f ? 0f : random.Float(0f, 0.33f * height3)
            );
            _treeBrushesByType[1].Add(item2);
        }

        _treeBrushesByType[2] = [];
        for (var k = 0; k < 16; k++)
        {
            var array3 = new[]
            {
                7, 8, 9, 10, 10, 11, 11, 12, 12, 13, 13, 14, 14, 15, 16, 17
            };
            var height2 = array3[k];
            var branchesCount3 = height2 * 3;
            var item3 = CreateTreeBrush(
                random,
                GetTreeTrunkValue(TreeType.Spruce),
                GetTreeLeavesValue(TreeType.Spruce),
                height2,
                branchesCount3,
                3,
                delegate(int y, int _)
                {
                    var leafChance = MathUtils.Lerp(1.4f, 0.3f, y / (float)height2);
                    if (y < 3)
                    {
                        leafChance = 0f;
                    }

                    if (y % 2 == 0)
                    {
                        leafChance *= 0.3f;
                    }

                    return leafChance;
                },
                delegate(int y)
                {
                    if (y < 3 || y > height2 * 0.8f)
                    {
                        return 0f;
                    }

                    return y % 2 == 0 ? 0f : MathUtils.Lerp(0.3f * height2, 0f, MathUtils.Saturate(y / (float)height2));
                }
            );
            _treeBrushesByType[2].Add(item3);
        }

        _treeBrushesByType[3] = [];
        for (var l = 0; l < 16; l++)
        {
            var array4 = new[]
            {
                20, 21, 22, 23, 24, 24, 25, 25, 26, 26, 27, 27, 28, 28, 29, 30, 31, 32
            };
            var height = array4[l];
            var branchesCount4 = height * 3;
            var startHeight = (0.3f + l % 4 * 0.05f) * height;
            var item4 = CreateTreeBrush(
                random,
                GetTreeTrunkValue(TreeType.TallSpruce),
                GetTreeLeavesValue(TreeType.TallSpruce),
                height,
                branchesCount4,
                3,
                delegate(int y, int _)
                {
                    var relativeHeight = MathUtils.Saturate(y / (float)height);
                    var leafChance = MathUtils.Lerp(1.5f, 0f, MathUtils.Saturate((relativeHeight - 0.6f) / 0.4f));
                    if (y < startHeight)
                    {
                        leafChance = 0f;
                    }

                    if (y % 3 != 0 && y < height - 4)
                    {
                        leafChance *= 0.2f;
                    }

                    return leafChance;
                },
                delegate(int y)
                {
                    var relativeHeight = MathUtils.Saturate(y / (float)height);
                    if (y % 3 != 0)
                    {
                        return 0f;
                    }

                    return y < startHeight
                        ? !(y < startHeight - 4f) ? 0.1f * height : 0f
                        : MathUtils.Lerp(0.18f * height, 0f, MathUtils.Saturate((relativeHeight - 0.6f) / 0.4f));
                }
            );
            _treeBrushesByType[3].Add(item4);
        }

        _treeBrushesByType[4] = [];
        for (var m = 0; m < 16; m++)
        {
            _treeBrushesByType[4].Add(CreateMimosaBrush(random, MathUtils.Lerp(6f, 9f, m / 15f)));
        }

        _treeBrushesByType[5] = [];
        for (var n = 0; n < 16; n++)
        {
            var array5 = new[]
            {
                10, 11, 11, 12, 12, 13, 13, 13, 14, 14, 14, 15, 15, 16, 16, 17, 17
            };
            var height5 = array5[n];
            var branchesCount5 = height5 * 3;
            var item5 = CreateTreeBrush(
                random,
                GetTreeTrunkValue(TreeType.Poplar),
                GetTreeLeavesValue(TreeType.Poplar),
                height5,
                branchesCount5,
                2,
                delegate(int y, int round)
                {
                    var canopyBaseY = height5 < 14 ? 1 : 2;
                    if (y < canopyBaseY)
                    {
                        return 0f;
                    }

                    if (round == 0)
                    {
                        return 1f;
                    }

                    if (y == canopyBaseY)
                    {
                        return 0f;
                    }

                    return y == canopyBaseY + 1 ? 0.5f : MathUtils.LinearStep(height5 - 1, canopyBaseY + 2, y);
                },
                _ => 0f
            );
            _treeBrushesByType[5].Add(item5);
        }
    }

    public static int GetTreeTrunkValue(TreeType treeType)
    {
        return _treeTrunksByType[(int)treeType];
    }

    public static int GetTreeLeavesValue(TreeType treeType)
    {
        return _treeLeavesByType[(int)treeType];
    }

    public static ReadOnlyList<TerrainBrush> GetTreeBrushes(TreeType treeType)
    {
        return new ReadOnlyList<TerrainBrush>(_treeBrushesByType[(int)treeType]);
    }

    public static int GenerateRandomPlantValue(Random random, int groundValue, int temperature, int humidity, int y)
    {
        switch (Terrain.ExtractContents(groundValue))
        {
            case 2:
            case 8:
                if (humidity >= 6)
                {
                    if (!(random.Float(0f, 1f) < humidity / 60f))
                    {
                        break;
                    }

                    var result = Terrain.MakeBlockValue(19, TallGrassBlock.SetIsSmall(0, false));
                    if (SubsystemWeather.IsPlaceFrozen(temperature, y))
                    {
                        return result;
                    }

                    var plantRoll = random.Float(0f, 1f);
                    result = plantRoll switch
                    {
                        < 0.04f => Terrain.MakeBlockValue(20),
                        < 0.07f => Terrain.MakeBlockValue(24),
                        < 0.09f => Terrain.MakeBlockValue(25),
                        < 0.17f => Terrain.MakeBlockValue(174, RyeBlock.SetIsWild(RyeBlock.SetSize(0, 7), true)),
                        < 0.19f => Terrain.MakeBlockValue(
                            204,
                            CottonBlock.SetIsWild(CottonBlock.SetSize(0, 2), true)
                        ),
                        _ => result
                    };

                    return result;
                }

                if (random.Float(0f, 1f) < 0.025f)
                {
                    return Terrain.MakeBlockValue(random.Float(0f, 1f) < 0.2f ? 99 : 28, 0);
                }

                break;
            case 7:
                if (humidity < 8 && random.Float(0f, 1f) < 0.01f)
                {
                    return Terrain.MakeBlockValue(random.Float(0f, 1f) < 0.05f ? 99 : 28, 0);
                }

                break;
        }

        return 0;
    }

    public static TreeType? GenerateRandomTreeType(
        Random random,
        int temperature,
        int humidity,
        int y,
        float densityMultiplier = 1f
    )
    {
        TreeType? result = null;
        var oakScore = random.Float() * CalculateTreeProbability(TreeType.Oak, temperature, humidity, y);
        var birchScore = random.Float() * CalculateTreeProbability(TreeType.Birch, temperature, humidity, y);
        var spruceScore = random.Float() * CalculateTreeProbability(TreeType.Spruce, temperature, humidity, y);
        var tallSpruceScore = random.Float() * CalculateTreeProbability(TreeType.TallSpruce, temperature, humidity, y);
        var mimosaScore = random.Float() * CalculateTreeProbability(TreeType.Mimosa, temperature, humidity, y);
        var poplarScore = random.Float() * CalculateTreeProbability(TreeType.Poplar, temperature, humidity, y);
        var bestScore = MathUtils.Max(
            MathUtils.Max(oakScore, birchScore, spruceScore, tallSpruceScore),
            mimosaScore,
            poplarScore
        );
        if (bestScore > 0f)
        {
            if (bestScore.CloseTo(oakScore))
            {
                result = TreeType.Oak;
            }

            if (bestScore.CloseTo(birchScore))
            {
                result = TreeType.Birch;
            }

            if (bestScore.CloseTo(spruceScore))
            {
                result = TreeType.Spruce;
            }

            if (bestScore.CloseTo(tallSpruceScore))
            {
                result = TreeType.TallSpruce;
            }

            if (bestScore.CloseTo(mimosaScore))
            {
                result = TreeType.Mimosa;
            }

            if (bestScore.CloseTo(poplarScore))
            {
                result = TreeType.Poplar;
            }
        }

        if (result.HasValue &&
            random.Bool(densityMultiplier * CalculateTreeDensity(result.Value, temperature, humidity, y)))
        {
            return result;
        }

        return null;
    }

    public static float CalculateTreeDensity(TreeType treeType, int temperature, int humidity, int y)
    {
        return treeType switch
        {
            TreeType.Oak => RangeProbability(humidity, 4f, 15f, 15f, 15f),
            TreeType.Birch => RangeProbability(humidity, 4f, 15f, 15f, 15f),
            TreeType.Spruce => RangeProbability(humidity, 4f, 15f, 15f, 15f),
            TreeType.TallSpruce => RangeProbability(humidity, 4f, 15f, 15f, 15f),
            TreeType.Mimosa => 0.04f,
            TreeType.Poplar => RangeProbability(temperature, 4f, 8f, 10f, 15f) *
                               RangeProbability(humidity, 3f, 15f, 15f, 15f) *
                               RangeProbability(y, 0f, 0f, 85f, 92f),
            _ => 0f
        };
    }

    public static float CalculateTreeProbability(TreeType treeType, int temperature, int humidity, int y)
    {
        return treeType switch
        {
            TreeType.Oak => RangeProbability(temperature, 4f, 10f, 15f, 15f) *
                            RangeProbability(humidity, 6f, 8f, 15f, 15f) *
                            RangeProbability(y, 0f, 0f, 82f, 87f),
            TreeType.Birch => RangeProbability(temperature, 5f, 9f, 11f, 15f) *
                              RangeProbability(humidity, 3f, 15f, 15f, 15f) *
                              RangeProbability(y, 0f, 0f, 82f, 87f),
            TreeType.Spruce => RangeProbability(temperature, 0f, 0f, 6f, 10f) *
                               RangeProbability(humidity, 3f, 10f, 11f, 12f),
            TreeType.TallSpruce => 0.25f *
                                   RangeProbability(temperature, -100f, -100f, 6f, 10f) *
                                   RangeProbability(humidity, 2f, 11f, 15f, 15f) *
                                   RangeProbability(y, 0f, 0f, 310f, 312f),
            TreeType.Mimosa => RangeProbability(temperature, 2f, 4f, 12f, 14f) *
                               RangeProbability(humidity, 0f, 0f, 4f, 6f),
            TreeType.Poplar => RangeProbability(temperature, 4f, 8f, 12f, 15f) *
                               RangeProbability(humidity, 3f, 15f, 15f, 15f) *
                               RangeProbability(y, 0f, 0f, 85f, 92f),
            _ => 0f
        };
    }

    private static float RangeProbability(float v, float a, float b, float c, float d)
    {
        if (v < a)
        {
            return 0f;
        }

        if (v < b)
        {
            return (v - a) / (b - a);
        }

        if (v <= c)
        {
            return 1f;
        }

        if (v <= d)
        {
            return 1f - (v - c) / (d - c);
        }

        return 0f;
    }

    private static TerrainBrush CreateTreeBrush(
        Random random,
        int woodIndex,
        int leavesIndex,
        int height,
        int branchesCount,
        int leavesRounds,
        Func<int, int, float> leavesProbability,
        Func<int, float> branchesLength
    )
    {
        var terrainBrush = new TerrainBrush();
        terrainBrush.AddRay(0, -1, 0, 0, height, 0, 1, 1, 1, woodIndex);
        for (var i = 0; i < branchesCount; i++)
        {
            var x = 0;
            var branchBaseY = random.Int(0, height);
            var z = 0;
            var s = branchesLength(branchBaseY);
            var vector =
                Vector3.Normalize(new Vector3(random.Float(-1f, 1f), random.Float(0f, 0.33f), random.Float(-1f, 1f))) *
                s;
            var x2 = (int)MathUtils.Round(vector.X);
            var y = branchBaseY + (int)MathUtils.Round(vector.Y);
            var z2 = (int)MathUtils.Round(vector.Z);
            var cutFace = 0;
            if (MathUtils.Abs(vector.X).CloseTo(
                    MathUtils.Max(MathUtils.Abs(vector.X), MathUtils.Abs(vector.Y), MathUtils.Abs(vector.Z))
                ))
            {
                cutFace = 1;
            }
            else if (MathUtils.Abs(vector.Y).CloseTo(
                         MathUtils.Max(MathUtils.Abs(vector.X), MathUtils.Abs(vector.Y), MathUtils.Abs(vector.Z))
                     ))
            {
                cutFace = 4;
            }

            terrainBrush.AddRay(
                x,
                branchBaseY,
                z,
                x2,
                y,
                z2,
                1,
                1,
                1,
                (Func<int?, int?>)(v =>
                    v.HasValue
                        ? null
                        : new int?(Terrain.MakeBlockValue(woodIndex, WoodBlock.SetCutFace(0, cutFace))))
            );
        }

        for (var j = 0; j < leavesRounds; j++)
        {
            terrainBrush.CalculateBounds(out var min, out var max);
            for (var k = min.X - 1; k <= max.X + 1; k++)
            {
                for (var l = min.Z - 1; l <= max.Z + 1; l++)
                {
                    for (var m = 1; m <= max.Y + 1; m++)
                    {
                        var leafChance = leavesProbability(m, j);
                        if (random.Float(0f, 1f) < leafChance &&
                            !terrainBrush.GetValue(k, m, l).HasValue &&
                            (terrainBrush.CountNonDiagonalNeighbors(k, m, l, leavesIndex) != 0 ||
                             terrainBrush.CountNonDiagonalNeighbors(
                                 k,
                                 m,
                                 l,
                                 (Func<int?, int>)(v =>
                                     v.HasValue && Terrain.ExtractContents(v.Value) == woodIndex ? 1 : 0)
                             ) !=
                             0))
                        {
                            terrainBrush.AddCell(k, m, l, 0);
                        }
                    }
                }
            }

            terrainBrush.Replace(0, leavesIndex);
        }

        terrainBrush.AddCell(0, height, 0, leavesIndex);
        terrainBrush.Compile();
        return terrainBrush;
    }

    private static TerrainBrush CreateMimosaBrush(Random random, float size)
    {
        var terrainBrush = new TerrainBrush();
        var value = _treeTrunksByType[4];
        var value2 = _treeLeavesByType[4];
        terrainBrush.AddRay(0, -1, 0, 0, 0, 0, 1, 1, 1, value);
        var list = new List<Point3>();
        var azimuthOffset = random.Float(0f, (float)Math.PI * 2f);
        for (var i = 0; i < 3; i++)
        {
            var radians = azimuthOffset + i * MathUtils.DegToRad(120f);
            var v = Vector3.Transform(
                Vector3.Normalize(new Vector3(1f, random.Float(1f, 1.5f), 0f)),
                Matrix.CreateRotationY(radians)
            );
            var branchLength = random.Int((int)(0.7f * size), (int)size);
            var p = new Point3(0, 0, 0);
            var item = new Point3(Vector3.Round(new Vector3(p) + v * branchLength));
            terrainBrush.AddRay(p.X, p.Y, p.Z, item.X, item.Y, item.Z, 1, 1, 1, value);
            list.Add(item);
        }

        foreach (var item2 in list)
        {
            var crownRadius = random.Float(0.3f * size, 0.45f * size);
            var searchRadius = (int)MathUtils.Ceiling(crownRadius);
            for (var j = item2.X - searchRadius; j <= item2.X + searchRadius; j++)
            {
                for (var k = item2.Y - searchRadius; k <= item2.Y + searchRadius; k++)
                {
                    for (var l = item2.Z - searchRadius; l <= item2.Z + searchRadius; l++)
                    {
                        var manhattanDistance = Math.Abs(j - item2.X) + Math.Abs(k - item2.Y) + Math.Abs(l - item2.Z);
                        var scaledDistance = ((new Vector3(j, k, l) - new Vector3(item2)) * new Vector3(1f, 1.7f, 1f))
                            .Length();
                        if (scaledDistance <= crownRadius &&
                            (crownRadius - scaledDistance > 1f || manhattanDistance <= 2 || random.Bool(0.7f)) &&
                            !terrainBrush.GetValue(j, k, l).HasValue)
                        {
                            terrainBrush.AddCell(j, k, l, value2);
                        }
                    }
                }
            }
        }

        terrainBrush.Compile();
        return terrainBrush;
    }
}
