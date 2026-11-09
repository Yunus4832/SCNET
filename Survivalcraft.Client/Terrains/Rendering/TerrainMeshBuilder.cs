using Engine.Graphics;

using Game.Blocks.Presentation.Geometry;

namespace Game.Terrains.Rendering;

/// <summary>CPU-only slice-cache builder; uploads remain on the rendering thread.</summary>
internal sealed class TerrainMeshBuilder(Terrain terrain, BlockGeometryGenerator generator, Texture2D texture)
{
    private readonly Terrain _terrain = terrain;

    private readonly BlockGeometryGenerator _generator = generator;

    private readonly Texture2D _texture = texture;

    internal void GeneratePass(TerrainChunk chunk, TerrainChunkPresentation output, bool even)
    {
        _generator.ResetCache();
        if (!output.Draws.TryGetValue(
                _texture,
                out var terrainGeometry
            ))
        {
            terrainGeometry = new TerrainGeometry[TerrainChunk.SlicesCount];
            for (var i = 0; i < TerrainChunk.SlicesCount; i++)
            {
                var t = new TerrainGeometry(output.Draws, i);
                terrainGeometry[i] = t;
            }

            output.Draws.Add(
                _texture,
                terrainGeometry
            );
        }

        var chunkAtCoords = _terrain.GetChunkAtCoords(chunk.Coords.X - 1, chunk.Coords.Y - 1);
        var chunkAtCoords2 = _terrain.GetChunkAtCoords(chunk.Coords.X, chunk.Coords.Y - 1);
        var chunkAtCoords3 = _terrain.GetChunkAtCoords(chunk.Coords.X + 1, chunk.Coords.Y - 1);
        var chunkAtCoords4 = _terrain.GetChunkAtCoords(chunk.Coords.X - 1, chunk.Coords.Y);
        var chunkAtCoords5 = _terrain.GetChunkAtCoords(chunk.Coords.X + 1, chunk.Coords.Y);
        var chunkAtCoords6 = _terrain.GetChunkAtCoords(chunk.Coords.X - 1, chunk.Coords.Y + 1);
        var chunkAtCoords7 = _terrain.GetChunkAtCoords(chunk.Coords.X, chunk.Coords.Y + 1);
        var chunkAtCoords8 = _terrain.GetChunkAtCoords(chunk.Coords.X + 1, chunk.Coords.Y + 1);
        var num = 0;
        var num2 = 0;
        var num3 = 16;
        var num4 = 16;
        if (chunkAtCoords4 == null)
        {
            num++;
        }

        if (chunkAtCoords2 == null)
        {
            num2++;
        }

        if (chunkAtCoords5 == null)
        {
            num3--;
        }

        if (chunkAtCoords7 == null)
        {
            num4--;
        }

        for (var i = 0; i < TerrainChunk.SlicesCount; i++)
        {
            if (i % 2 == 0 != even)
            {
                continue;
            }

            var contentsHash = CalculateChunkSliceContentsHash(chunk, i);
            var presentation = output;
            if (!presentation.BeginSliceGeometryUpdate(i, contentsHash))
            {
                continue;
            }

            foreach (var c in output.Draws)
            {
                var subsets = c.Value[i].Subsets;
                foreach (var subset in subsets)
                {
                    subset.Vertices.Clear();
                    subset.Indices.Clear();
                }
            }

            for (var k = num; k < num3; k++)
            {
                for (var l = num2; l < num4; l++)
                {
                    switch (k)
                    {
                        case 0:
                            if ((l == 0 && chunkAtCoords == null) || (l == 15 && chunkAtCoords6 == null))
                            {
                                continue;
                            }

                            break;
                        case 15:
                            if ((l == 0 && chunkAtCoords3 == null) || (l == 15 && chunkAtCoords8 == null))
                            {
                                continue;
                            }

                            break;
                    }

                    var num5 = k + chunk.Origin.X;
                    var num6 = l + chunk.Origin.Y;
                    var bottomHeightFast = chunk.GetBottomHeightFast(k, l);
                    var bottomHeight = _terrain.GetBottomHeight(num5 - 1, num6);
                    var bottomHeight2 = _terrain.GetBottomHeight(num5 + 1, num6);
                    var bottomHeight3 = _terrain.GetBottomHeight(num5, num6 - 1);
                    var bottomHeight4 = _terrain.GetBottomHeight(num5, num6 + 1);
                    var x = MathUtils.Min(
                        bottomHeightFast - 1,
                        MathUtils.Min(bottomHeight, bottomHeight2, bottomHeight3, bottomHeight4)
                    );
                    var x2 = chunk.GetTopHeightFast(k, l) + 1;
                    var num7 = MathUtils.Max(16 * i, x, 1);
                    var num8 = MathUtils.Min(16 * (i + 1), x2, 256);
                    var num9 = TerrainChunk.CalculateCellIndex(k, 0, l);
                    for (var m = num7; m < num8; m++)
                    {
                        var cellValueFast = chunk.GetCellValueForGeometry(num9 + m);
                        var num10 = Terrain.ExtractContents(cellValueFast);
                        if (num10 != 0)
                        {
                            _generator.Presentations.Get(num10).GenerateTerrainVertices(
                                _generator,
                                terrainGeometry[i],
                                cellValueFast,
                                num5,
                                m,
                                num6
                            );
                        }
                    }
                }
            }

            presentation.CompleteSliceGeometryUpdate(i, contentsHash);
        }
    }

    internal int CalculateChunkSliceContentsHash(TerrainChunk chunk, int sliceIndex)
    {
        var num = 1;
        var num2 = chunk.Origin.X - 1;
        var num3 = chunk.Origin.X + 16 + 1;
        var num4 = chunk.Origin.Y - 1;
        var num5 = chunk.Origin.Y + 16 + 1;
        var x = MathUtils.Max(16 * sliceIndex - 1, 0);
        var x2 = MathUtils.Min(16 * (sliceIndex + 1) + 1, 256);
        for (var i = num2; i < num3; i++)
        {
            for (var j = num4; j < num5; j++)
            {
                var chunkAtCell = _terrain.GetChunkAtCell(i, j, false);
                if (chunkAtCell == null)
                {
                    continue;
                }

                var x3 = i & 0xF;
                var z = j & 0xF;
                var shaftValueFast = chunkAtCell.GetShaftValueFast(x3, z);
                var num6 = Terrain.ExtractBottomHeight(shaftValueFast);
                var num7 = Terrain.ExtractTopHeight(shaftValueFast);
                var num8 = MathUtils.Max(x, num6 - 1);
                var num9 = MathUtils.Min(x2, num7 + 2);
                var num10 = TerrainChunk.CalculateCellIndex(x3, num8, z);
                var num11 = num10 + num9 - num8;
                while (num10 < num11)
                {
                    num += chunkAtCell.GetCellValueForGeometry(num10++);
                    num *= 31;
                }

                num += Terrain.ExtractTemperature(shaftValueFast);
                num *= 31;
                num += Terrain.ExtractHumidity(shaftValueFast);
                num *= 31;
                num += num8;
                num *= 31;
            }
        }

        num += _terrain.SeasonTemperature;
        num *= 31;
        num += _terrain.SeasonHumidity;
        num *= 31;
        return num;
    }
}
