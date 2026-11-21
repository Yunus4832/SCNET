namespace Game.Terrains.Generation;

public partial class TerrainContentsGenerator
{
    public void GenerateTraps(TerrainChunk chunk)
    {
        if (!tgExtras)
        {
            return;
        }

        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        var random = new Random(seed + x + 2113 * y);
        if (!random.Bool(0.15f) || !(CalculateOceanShoreDistance(chunk.Origin.X, chunk.Origin.Y) > 50f))
        {
            return;
        }

        var num = random.Int(0, MathUtils.Max(2, 1));
        for (var i = 0; i < num; i++)
        {
            var num2 = random.Int(2, 5);
            var num3 = random.Int(2, 5);
            var num4 = random.Int(1, 16 - num2 - 2);
            var num5 = random.Int(1, 16 - num3 - 2);
            var flag = random.Float(0f, 1f) < 0.5f;
            var num6 = random.Int(3, 5);
            int? num7 = null;
            var num8 = num4 - 1;
            while (true)
            {
                if (num8 < num4 + num2 + 1)
                {
                    for (var j = num5 - 1; j < num5 + num3 + 1; j++)
                    {
                        var num9 = chunk.CalculateTopmostCellHeight(num8, j);
                        var num10 = MathUtils.Max(num9 - 20, 5);
                        while (num9 >= num10 && chunk.GetCellContentsFast(num8, num9, j) != 8)
                        {
                            num9--;
                        }

                        if (num7.HasValue && num7 != num9)
                        {
                            goto end_IL_019b;
                        }

                        num7 = num9;
                        if (chunk.GetCellContentsFast(num8, num9, j) != 8)
                        {
                            goto end_IL_019b;
                        }
                    }

                    num8++;
                    continue;
                }

                if (!num7.HasValue || num7 - num6 < 5)
                {
                    break;
                }

                for (var k = num4; k < num4 + num2; k++)
                {
                    for (var l = num5; l < num5 + num3; l++)
                    {
                        for (var num11 = num7.Value - 1; num11 >= num7 - num6 + 1; num11--)
                        {
                            chunk.SetCellValueFast(k, num11, l, Terrain.MakeBlockValue(0));
                        }

                        chunk.SetCellValueFast(k, num7.Value, l, Terrain.MakeBlockValue(87));
                        if (!flag)
                        {
                            continue;
                        }

                        var data = SpikedPlankBlock.SetSpikesState(0, random.Float(0f, 1f) < 0.33f);
                        chunk.SetCellValueFast(k, num7.Value - num6 + 1, l, Terrain.MakeBlockValue(86, data));
                    }
                }

                break;
                end_IL_019b:
                break;
            }
        }
    }

    public void GenerateGraves(TerrainChunk chunk)
    {
        if (!tgExtras)
        {
            return;
        }

        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        var random = new Random((int)MathUtils.Hash((uint)(seed + x + 10323 * y)));
        if (!(random.Float(0f, 1f) < 0.033f) ||
            !(CalculateOceanShoreDistance(chunk.Origin.X, chunk.Origin.Y) > 10f))
        {
            return;
        }

        var num = random.Int(0, MathUtils.Max(1, 1));
        for (var i = 0; i < num; i++)
        {
            var num2 = random.Int(6, 9);
            var num3 = random.Int(6, 9);
            var num4 = random.Bool(0.2f) ? random.Int(6, 20) : random.Int(1, 5);
            var flag = random.Bool(0.5f);
            for (var j = 0; j < num4; j++)
            {
                var num5 = num2 + random.Int(-4, 4);
                var num6 = num3 + random.Int(-4, 4);
                var num7 = chunk.CalculateTopmostCellHeight(num5, num6);
                if (num7 is < 10 or > 246)
                {
                    continue;
                }

                var num8 = random.Int(0, 3);
                for (var k = 0; k < 4; k++)
                {
                    var num9 = (k + num8) % 4;
                    var p = CellFace.FaceToPoint3(num9);
                    var p2 = new Point3(-p.Z, p.Y, p.X);
                    var num10 = p.X < 0 ? num5 - 2 : num5 - 1;
                    var num11 = p.X > 0 ? num5 + 2 : num5 + 1;
                    var num12 = p.Z < 0 ? num6 - 2 : num6 - 1;
                    var num13 = p.Z > 0 ? num6 + 2 : num6 + 1;
                    for (var l = num10; l <= num11; l++)
                    {
                        for (var m = num7 - 2; m <= num7 + 2; m++)
                        {
                            for (var n = num12; n <= num13; n++)
                            {
                                var num14 = Terrain.ExtractContents(chunk.GetCellValueFast(l, m, n));
                                var block = _subsystemTerrain.Catalog.Blocks[num14];
                                if (m > num7)
                                {
                                    if (!block.Collidable)
                                    {
                                        continue;
                                    }
                                }
                                else if (num14 is 8 or 2 or 7 or 3 or 4)
                                {
                                    continue;
                                }

                                goto IL_06ac;
                            }
                        }
                    }

                    var num15 = random.Int(0, 7);
                    var data = GravestoneBlock.SetVariant(GravestoneBlock.SetRotation(0, num9 % 2), num15);
                    int? num16 = null;
                    var contents = 217;
                    var contents2 = 136;
                    if (num15 >= 4 && !flag)
                    {
                        var cellContentsFast = chunk.GetCellContentsFast(num5, num7, num6);
                        if (cellContentsFast == 7 || cellContentsFast == 4)
                        {
                            num16 = Terrain.MakeBlockValue(4);
                            contents = 51;
                            contents2 = 52;
                        }
                        else if (random.Float(0f, 1f) < 0.5f)
                        {
                            num16 = Terrain.MakeBlockValue(3);
                            contents = 217;
                            contents2 = 136;
                        }
                        else
                        {
                            num16 = Terrain.MakeBlockValue(67);
                            contents = 96;
                            contents2 = 95;
                        }
                    }

                    var flag2 = num16.HasValue && random.Bool(0.33f);
                    var num17 = random.Float(0f, 1f);
                    var num18 = random.Float(0f, 1f);
                    var num19 = random.Int(-1, 0);
                    var num20 = random.Int(1, 2);
                    var num21 = flag2 ? num7 + 2 : num7 + 1;
                    chunk.SetCellValueFast(num5, num21, num6, Terrain.MakeBlockValue(189, data));
                    for (var num22 = num19; num22 <= num20; num22++)
                    {
                        var num23 = num5 + p.X * num22;
                        var num24 = num6 + p.Z * num22;
                        if (num22 is 0 or 1)
                        {
                            chunk.SetCellValueFast(num23, num21 - 2, num24, Terrain.MakeBlockValue(190));
                            if (num16.HasValue)
                            {
                                chunk.SetCellValueFast(num23, num21 - 1, num24, num16.Value);
                                if (num22 == 1)
                                {
                                    var num25 = 0;
                                    if (num18 < 0.2f)
                                    {
                                        num25 = Terrain.MakeBlockValue(20);
                                    }
                                    else if (num18 < 0.3f)
                                    {
                                        num25 = Terrain.MakeBlockValue(24);
                                    }
                                    else if (num18 < 0.4f)
                                    {
                                        num25 = Terrain.MakeBlockValue(25);
                                    }
                                    else if (num18 < 0.5f)
                                    {
                                        num25 = Terrain.MakeBlockValue(31, 4);
                                    }
                                    else if (num18 < 0.6f)
                                    {
                                        num25 = Terrain.MakeBlockValue(132, CellFace.OppositeFace(num9));
                                    }

                                    if (num25 != 0)
                                    {
                                        chunk.SetCellValueFast(num23, num21, num24, num25);
                                    }
                                }
                            }
                        }

                        if (!flag2)
                        {
                            continue;
                        }

                        if (num17 < 0.3f)
                        {
                            var value = Terrain.MakeBlockValue(
                                contents,
                                StairsBlock.SetRotation(0, CellFace.Point3ToFace(p2))
                            );
                            var value2 = Terrain.MakeBlockValue(
                                contents,
                                StairsBlock.SetRotation(0, CellFace.OppositeFace(CellFace.Point3ToFace(p2)))
                            );
                            chunk.SetCellValueFast(num23 + p2.X, num21 - 1, num24 + p2.Z, value);
                            chunk.SetCellValueFast(num23 - p2.X, num21 - 1, num24 - p2.Z, value2);
                            if (num22 == -1)
                            {
                                var value3 = Terrain.MakeBlockValue(
                                    contents,
                                    StairsBlock.SetRotation(0, CellFace.OppositeFace(CellFace.Point3ToFace(p)))
                                );
                                chunk.SetCellValueFast(num23, num21 - 1, num24, value3);
                            }

                            if (num22 == 2)
                            {
                                var value4 = Terrain.MakeBlockValue(
                                    contents,
                                    StairsBlock.SetRotation(0, CellFace.Point3ToFace(p))
                                );
                                chunk.SetCellValueFast(num23, num21 - 1, num24, value4);
                            }
                        }
                        else if (num17 < 0.4f)
                        {
                            chunk.SetCellValueFast(
                                num23 + p2.X,
                                num21 - 1,
                                num24 + p2.Z,
                                Terrain.MakeBlockValue(contents2)
                            );
                            chunk.SetCellValueFast(
                                num23 - p2.X,
                                num21 - 1,
                                num24 - p2.Z,
                                Terrain.MakeBlockValue(contents2)
                            );
                            if (num22 == -1)
                            {
                                chunk.SetCellValueFast(num23, num21 - 1, num24, Terrain.MakeBlockValue(contents2));
                            }

                            if (num22 == 2)
                            {
                                chunk.SetCellValueFast(num23, num21 - 1, num24, Terrain.MakeBlockValue(contents2));
                            }
                        }
                        else if (num17 < 0.6f)
                        {
                            if (num22 is 0 or 1)
                            {
                                chunk.SetCellValueFast(
                                    num23 + p2.X,
                                    num21 - 1,
                                    num24 + p2.Z,
                                    Terrain.MakeBlockValue(31, CellFace.Point3ToFace(p2))
                                );
                                chunk.SetCellValueFast(
                                    num23 - p2.X,
                                    num21 - 1,
                                    num24 - p2.Z,
                                    Terrain.MakeBlockValue(31, CellFace.OppositeFace(CellFace.Point3ToFace(p2)))
                                );
                            }

                            if (num22 == -1)
                            {
                                chunk.SetCellValueFast(
                                    num23,
                                    num21 - 1,
                                    num24,
                                    Terrain.MakeBlockValue(31, CellFace.OppositeFace(num9))
                                );
                            }

                            if (num22 == 2)
                            {
                                chunk.SetCellValueFast(num23, num21 - 1, num24, Terrain.MakeBlockValue(31, num9));
                            }
                        }
                    }

                    break;
                    IL_06ac: ;
                }
            }
        }
    }

    public void GenerateCairns(TerrainChunk chunk)
    {
        var num = 190;
        var point = default(Point2);
        for (var i = 0; i < 16; i++)
        {
            for (var j = 0; j < 16; j++)
            {
                if (Terrain.ExtractContents(chunk.GetCellValueFast(i, num, j)) == 0)
                {
                    continue;
                }

                for (var k = num + 1; k < 256; k++)
                {
                    if (Terrain.ExtractContents(chunk.GetCellValueFast(i, k, j)) != 0)
                    {
                        continue;
                    }

                    num = k;
                    point = new Point2(i, j);
                    break;
                }
            }
        }

        if (num is < 190 or > 255 || point.X is < 1 or >= 15 || point.Y is < 1 or >= 15)
        {
            return;
        }

        var data = MathUtils.Clamp((int)(4f * MathUtils.LinearStep(190f, 256f, num)), 0, 3);
        chunk.SetCellValueFast(point.X, num, point.Y, Terrain.MakeBlockValue(258, data));
    }
}
