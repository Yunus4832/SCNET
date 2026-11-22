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

        var trapAttempts = random.Int(0, MathUtils.Max(2, 1));
        for (var i = 0; i < trapAttempts; i++)
        {
            var pitWidth = random.Int(2, 5);
            var pitLength = random.Int(2, 5);
            var startX = random.Int(1, 16 - pitWidth - 2);
            var startZ = random.Int(1, 16 - pitLength - 2);
            var placeSpikes = random.Float(0f, 1f) < 0.5f;
            var pitDepth = random.Int(3, 5);
            int? groundY = null;
            var scanX = startX - 1;
            while (true)
            {
                if (scanX < startX + pitWidth + 1)
                {
                    for (var j = startZ - 1; j < startZ + pitLength + 1; j++)
                    {
                        var scanY = chunk.CalculateTopmostCellHeight(scanX, j);
                        var minSearchY = MathUtils.Max(scanY - 20, 5);
                        while (scanY >= minSearchY && chunk.GetCellContentsFast(scanX, scanY, j) != 8)
                        {
                            scanY--;
                        }

                        if (groundY.HasValue && groundY != scanY)
                        {
                            goto end_IL_019b;
                        }

                        groundY = scanY;
                        if (chunk.GetCellContentsFast(scanX, scanY, j) != 8)
                        {
                            goto end_IL_019b;
                        }
                    }

                    scanX++;
                    continue;
                }

                if (!groundY.HasValue || groundY - pitDepth < 5)
                {
                    break;
                }

                for (var k = startX; k < startX + pitWidth; k++)
                {
                    for (var l = startZ; l < startZ + pitLength; l++)
                    {
                        for (var pitY = groundY.Value - 1; pitY >= groundY - pitDepth + 1; pitY--)
                        {
                            chunk.SetCellValueFast(k, pitY, l, Terrain.MakeBlockValue(0));
                        }

                        chunk.SetCellValueFast(k, groundY.Value, l, Terrain.MakeBlockValue(87));
                        if (!placeSpikes)
                        {
                            continue;
                        }

                        var data = SpikedPlankBlock.SetSpikesState(0, random.Float(0f, 1f) < 0.33f);
                        chunk.SetCellValueFast(k, groundY.Value - pitDepth + 1, l, Terrain.MakeBlockValue(86, data));
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

        var clusterAttempts = random.Int(0, MathUtils.Max(1, 1));
        for (var i = 0; i < clusterAttempts; i++)
        {
            var centerX = random.Int(6, 9);
            var centerZ = random.Int(6, 9);
            var graveAttempts = random.Bool(0.2f) ? random.Int(6, 20) : random.Int(1, 5);
            var skipFoundation = random.Bool(0.5f);
            for (var j = 0; j < graveAttempts; j++)
            {
                var graveX = centerX + random.Int(-4, 4);
                var graveZ = centerZ + random.Int(-4, 4);
                var groundY = chunk.CalculateTopmostCellHeight(graveX, graveZ);
                if (groundY is < 10 or > 246)
                {
                    continue;
                }

                var firstFace = random.Int(0, 3);
                for (var k = 0; k < 4; k++)
                {
                    var face = (k + firstFace) % 4;
                    var p = CellFace.FaceToPoint3(face);
                    var p2 = new Point3(-p.Z, p.Y, p.X);
                    var minX = p.X < 0 ? graveX - 2 : graveX - 1;
                    var maxX = p.X > 0 ? graveX + 2 : graveX + 1;
                    var minZ = p.Z < 0 ? graveZ - 2 : graveZ - 1;
                    var maxZ = p.Z > 0 ? graveZ + 2 : graveZ + 1;
                    for (var l = minX; l <= maxX; l++)
                    {
                        for (var m = groundY - 2; m <= groundY + 2; m++)
                        {
                            for (var n = minZ; n <= maxZ; n++)
                            {
                                var contentsAtCell = Terrain.ExtractContents(chunk.GetCellValueFast(l, m, n));
                                var block = _subsystemTerrain.Catalog.Blocks[contentsAtCell];
                                if (m > groundY)
                                {
                                    if (!block.Collidable)
                                    {
                                        continue;
                                    }
                                }
                                else if (contentsAtCell is 8 or 2 or 7 or 3 or 4)
                                {
                                    continue;
                                }

                                goto IL_06ac;
                            }
                        }
                    }

                    var gravestoneVariant = random.Int(0, 7);
                    var data = GravestoneBlock.SetVariant(GravestoneBlock.SetRotation(0, face % 2), gravestoneVariant);
                    int? foundationValue = null;
                    var stairsContents = 217;
                    var slabContents = 136;
                    if (gravestoneVariant >= 4 && !skipFoundation)
                    {
                        var cellContentsFast = chunk.GetCellContentsFast(graveX, groundY, graveZ);
                        if (cellContentsFast == 7 || cellContentsFast == 4)
                        {
                            foundationValue = Terrain.MakeBlockValue(4);
                            stairsContents = 51;
                            slabContents = 52;
                        }
                        else if (random.Float(0f, 1f) < 0.5f)
                        {
                            foundationValue = Terrain.MakeBlockValue(3);
                            stairsContents = 217;
                            slabContents = 136;
                        }
                        else
                        {
                            foundationValue = Terrain.MakeBlockValue(67);
                            stairsContents = 96;
                            slabContents = 95;
                        }
                    }

                    var raisedGrave = foundationValue.HasValue && random.Bool(0.33f);
                    var borderRoll = random.Float(0f, 1f);
                    var ornamentRoll = random.Float(0f, 1f);
                    var firstOffset = random.Int(-1, 0);
                    var lastOffset = random.Int(1, 2);
                    var gravestoneY = raisedGrave ? groundY + 2 : groundY + 1;
                    chunk.SetCellValueFast(graveX, gravestoneY, graveZ, Terrain.MakeBlockValue(189, data));
                    for (var lengthOffset = firstOffset; lengthOffset <= lastOffset; lengthOffset++)
                    {
                        var localX = graveX + p.X * lengthOffset;
                        var localZ = graveZ + p.Z * lengthOffset;
                        if (lengthOffset is 0 or 1)
                        {
                            chunk.SetCellValueFast(localX, gravestoneY - 2, localZ, Terrain.MakeBlockValue(190));
                            if (foundationValue.HasValue)
                            {
                                chunk.SetCellValueFast(localX, gravestoneY - 1, localZ, foundationValue.Value);
                                if (lengthOffset == 1)
                                {
                                    var ornamentValue = 0;
                                    if (ornamentRoll < 0.2f)
                                    {
                                        ornamentValue = Terrain.MakeBlockValue(20);
                                    }
                                    else if (ornamentRoll < 0.3f)
                                    {
                                        ornamentValue = Terrain.MakeBlockValue(24);
                                    }
                                    else if (ornamentRoll < 0.4f)
                                    {
                                        ornamentValue = Terrain.MakeBlockValue(25);
                                    }
                                    else if (ornamentRoll < 0.5f)
                                    {
                                        ornamentValue = Terrain.MakeBlockValue(31, 4);
                                    }
                                    else if (ornamentRoll < 0.6f)
                                    {
                                        ornamentValue = Terrain.MakeBlockValue(132, CellFace.OppositeFace(face));
                                    }

                                    if (ornamentValue != 0)
                                    {
                                        chunk.SetCellValueFast(localX, gravestoneY, localZ, ornamentValue);
                                    }
                                }
                            }
                        }

                        if (!raisedGrave)
                        {
                            continue;
                        }

                        if (borderRoll < 0.3f)
                        {
                            var value = Terrain.MakeBlockValue(
                                stairsContents,
                                StairsBlock.SetRotation(0, CellFace.Point3ToFace(p2))
                            );
                            var value2 = Terrain.MakeBlockValue(
                                stairsContents,
                                StairsBlock.SetRotation(0, CellFace.OppositeFace(CellFace.Point3ToFace(p2)))
                            );
                            chunk.SetCellValueFast(localX + p2.X, gravestoneY - 1, localZ + p2.Z, value);
                            chunk.SetCellValueFast(localX - p2.X, gravestoneY - 1, localZ - p2.Z, value2);
                            if (lengthOffset == -1)
                            {
                                var value3 = Terrain.MakeBlockValue(
                                    stairsContents,
                                    StairsBlock.SetRotation(0, CellFace.OppositeFace(CellFace.Point3ToFace(p)))
                                );
                                chunk.SetCellValueFast(localX, gravestoneY - 1, localZ, value3);
                            }

                            if (lengthOffset == 2)
                            {
                                var value4 = Terrain.MakeBlockValue(
                                    stairsContents,
                                    StairsBlock.SetRotation(0, CellFace.Point3ToFace(p))
                                );
                                chunk.SetCellValueFast(localX, gravestoneY - 1, localZ, value4);
                            }
                        }
                        else if (borderRoll < 0.4f)
                        {
                            chunk.SetCellValueFast(
                                localX + p2.X,
                                gravestoneY - 1,
                                localZ + p2.Z,
                                Terrain.MakeBlockValue(slabContents)
                            );
                            chunk.SetCellValueFast(
                                localX - p2.X,
                                gravestoneY - 1,
                                localZ - p2.Z,
                                Terrain.MakeBlockValue(slabContents)
                            );
                            if (lengthOffset == -1)
                            {
                                chunk.SetCellValueFast(
                                    localX,
                                    gravestoneY - 1,
                                    localZ,
                                    Terrain.MakeBlockValue(slabContents)
                                );
                            }

                            if (lengthOffset == 2)
                            {
                                chunk.SetCellValueFast(
                                    localX,
                                    gravestoneY - 1,
                                    localZ,
                                    Terrain.MakeBlockValue(slabContents)
                                );
                            }
                        }
                        else if (borderRoll < 0.6f)
                        {
                            if (lengthOffset is 0 or 1)
                            {
                                chunk.SetCellValueFast(
                                    localX + p2.X,
                                    gravestoneY - 1,
                                    localZ + p2.Z,
                                    Terrain.MakeBlockValue(31, CellFace.Point3ToFace(p2))
                                );
                                chunk.SetCellValueFast(
                                    localX - p2.X,
                                    gravestoneY - 1,
                                    localZ - p2.Z,
                                    Terrain.MakeBlockValue(31, CellFace.OppositeFace(CellFace.Point3ToFace(p2)))
                                );
                            }

                            if (lengthOffset == -1)
                            {
                                chunk.SetCellValueFast(
                                    localX,
                                    gravestoneY - 1,
                                    localZ,
                                    Terrain.MakeBlockValue(31, CellFace.OppositeFace(face))
                                );
                            }

                            if (lengthOffset == 2)
                            {
                                chunk.SetCellValueFast(
                                    localX,
                                    gravestoneY - 1,
                                    localZ,
                                    Terrain.MakeBlockValue(31, face)
                                );
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
        var cairnY = 190;
        var point = default(Point2);
        for (var i = 0; i < 16; i++)
        {
            for (var j = 0; j < 16; j++)
            {
                if (Terrain.ExtractContents(chunk.GetCellValueFast(i, cairnY, j)) == 0)
                {
                    continue;
                }

                for (var k = cairnY + 1; k < 256; k++)
                {
                    if (Terrain.ExtractContents(chunk.GetCellValueFast(i, k, j)) != 0)
                    {
                        continue;
                    }

                    cairnY = k;
                    point = new Point2(i, j);
                    break;
                }
            }
        }

        if (cairnY is < 190 or > 255 || point.X is < 1 or >= 15 || point.Y is < 1 or >= 15)
        {
            return;
        }

        var data = MathUtils.Clamp((int)(4f * MathUtils.LinearStep(190f, 256f, cairnY)), 0, 3);
        chunk.SetCellValueFast(point.X, cairnY, point.Y, Terrain.MakeBlockValue(258, data));
    }
}
