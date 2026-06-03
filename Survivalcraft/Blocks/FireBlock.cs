using Engine.Graphics;

namespace Game.Blocks;

public class FireBlock : Block
{
    public const int Index = 104;

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        var alphaTestSubsetsByFace = geometry.AlphaTestSubsetsByFace;
        var data = Terrain.ExtractData(value);
        var value2 = y + 1 < 256 ? generator.Terrain.GetCellValueFast(x, y + 1, z) : 0;
        var num = Terrain.ExtractContents(value2);
        var data2 = Terrain.ExtractData(value2);
        var value3 = y + 2 < 256 ? generator.Terrain.GetCellValueFast(x, y + 2, z) : 0;
        var num2 = Terrain.ExtractContents(value3);
        var data3 = Terrain.ExtractData(value3);
        if (HasFireOnFace(data, 0))
        {
            var value4 = y + 1 < 256 ? generator.Terrain.GetCellValueFast(x, y + 1, z + 1) : 0;
            var num3 = Terrain.ExtractContents(value4);
            var data4 = Terrain.ExtractData(value4);
            var value5 = y + 2 < 256 ? generator.Terrain.GetCellValueFast(x, y + 2, z + 1) : 0;
            var num4 = Terrain.ExtractContents(value5);
            var data5 = Terrain.ExtractData(value5);
            var num5 = TextureSlot;
            if ((num == 104 && HasFireOnFace(data2, 0)) || (num3 == 104 && HasFireOnFace(data4, 2)))
            {
                num5 += 16;
                if ((num2 == 104 && HasFireOnFace(data3, 0)) || (num4 == 104 && HasFireOnFace(data5, 2)))
                {
                    num5 += 16;
                }
            }

            var vertices = alphaTestSubsetsByFace[0].Vertices;
            var indices = alphaTestSubsetsByFace[0].Indices;
            var count = vertices.Count;
            vertices.Count += 4;
            BlockGeometryGenerator.SetupLitCornerVertex(x, y, z + 1, Color.White, num5, 0, ref vertices.Array[count]);
            BlockGeometryGenerator.SetupLitCornerVertex(x + 1, y, z + 1, Color.White, num5, 1,
                ref vertices.Array[count + 1]);
            BlockGeometryGenerator.SetupLitCornerVertex(x + 1, y + 1, z + 1, Color.White, num5, 2,
                ref vertices.Array[count + 2]);
            BlockGeometryGenerator.SetupLitCornerVertex(x, y + 1, z + 1, Color.White, num5, 3,
                ref vertices.Array[count + 3]);
            indices.Add(count);
            indices.Add(count + 1);
            indices.Add(count + 2);
            indices.Add(count + 2);
            indices.Add(count + 1);
            indices.Add(count);
            indices.Add(count + 2);
            indices.Add(count + 3);
            indices.Add(count);
            indices.Add(count);
            indices.Add(count + 3);
            indices.Add(count + 2);
        }

        if (HasFireOnFace(data, 1))
        {
            var value6 = y + 1 < 256 ? generator.Terrain.GetCellValueFast(x + 1, y + 1, z) : 0;
            var num6 = Terrain.ExtractContents(value6);
            var data6 = Terrain.ExtractData(value6);
            var value7 = y + 2 < 256 ? generator.Terrain.GetCellValueFast(x + 1, y + 2, z) : 0;
            var num7 = Terrain.ExtractContents(value7);
            var data7 = Terrain.ExtractData(value7);
            var num8 = TextureSlot;
            if ((num == 104 && HasFireOnFace(data2, 1)) || (num6 == 104 && HasFireOnFace(data6, 3)))
            {
                num8 += 16;
                if ((num2 == 104 && HasFireOnFace(data3, 1)) || (num7 == 104 && HasFireOnFace(data7, 3)))
                {
                    num8 += 16;
                }
            }

            var vertices2 = alphaTestSubsetsByFace[1].Vertices;
            var indices2 = alphaTestSubsetsByFace[1].Indices;
            var count2 = vertices2.Count;
            vertices2.Count += 4;
            BlockGeometryGenerator.SetupLitCornerVertex(x + 1, y, z, Color.White, num8, 0, ref vertices2.Array[count2]);
            BlockGeometryGenerator.SetupLitCornerVertex(x + 1, y + 1, z, Color.White, num8, 3,
                ref vertices2.Array[count2 + 1]);
            BlockGeometryGenerator.SetupLitCornerVertex(x + 1, y + 1, z + 1, Color.White, num8, 2,
                ref vertices2.Array[count2 + 2]);
            BlockGeometryGenerator.SetupLitCornerVertex(x + 1, y, z + 1, Color.White, num8, 1,
                ref vertices2.Array[count2 + 3]);
            indices2.Add(count2);
            indices2.Add(count2 + 1);
            indices2.Add(count2 + 2);
            indices2.Add(count2 + 2);
            indices2.Add(count2 + 1);
            indices2.Add(count2);
            indices2.Add(count2 + 2);
            indices2.Add(count2 + 3);
            indices2.Add(count2);
            indices2.Add(count2);
            indices2.Add(count2 + 3);
            indices2.Add(count2 + 2);
        }

        if (HasFireOnFace(data, 2))
        {
            var value8 = y + 1 < 256 ? generator.Terrain.GetCellValueFast(x, y + 1, z - 1) : 0;
            var num9 = Terrain.ExtractContents(value8);
            var data8 = Terrain.ExtractData(value8);
            var value9 = y + 2 < 256 ? generator.Terrain.GetCellValueFast(x, y + 2, z - 1) : 0;
            var num10 = Terrain.ExtractContents(value9);
            var data9 = Terrain.ExtractData(value9);
            var num11 = TextureSlot;
            if ((num == 104 && HasFireOnFace(data2, 2)) || (num9 == 104 && HasFireOnFace(data8, 0)))
            {
                num11 += 16;
                if ((num2 == 104 && HasFireOnFace(data3, 2)) || (num10 == 104 && HasFireOnFace(data9, 0)))
                {
                    num11 += 16;
                }
            }

            var vertices3 = alphaTestSubsetsByFace[2].Vertices;
            var indices3 = alphaTestSubsetsByFace[2].Indices;
            var count3 = vertices3.Count;
            vertices3.Count += 4;
            BlockGeometryGenerator.SetupLitCornerVertex(x, y, z, Color.White, num11, 0, ref vertices3.Array[count3]);
            BlockGeometryGenerator.SetupLitCornerVertex(x + 1, y, z, Color.White, num11, 1,
                ref vertices3.Array[count3 + 1]);
            BlockGeometryGenerator.SetupLitCornerVertex(x + 1, y + 1, z, Color.White, num11, 2,
                ref vertices3.Array[count3 + 2]);
            BlockGeometryGenerator.SetupLitCornerVertex(x, y + 1, z, Color.White, num11, 3,
                ref vertices3.Array[count3 + 3]);
            indices3.Add(count3);
            indices3.Add(count3 + 2);
            indices3.Add(count3 + 1);
            indices3.Add(count3 + 1);
            indices3.Add(count3 + 2);
            indices3.Add(count3);
            indices3.Add(count3 + 2);
            indices3.Add(count3);
            indices3.Add(count3 + 3);
            indices3.Add(count3 + 3);
            indices3.Add(count3);
            indices3.Add(count3 + 2);
        }

        if (!HasFireOnFace(data, 3))
        {
            return;
        }

        var value10 = y + 1 < 256 ? generator.Terrain.GetCellValueFast(x - 1, y + 1, z) : 0;
        var num12 = Terrain.ExtractContents(value10);
        var data10 = Terrain.ExtractData(value10);
        var value11 = y + 2 < 256 ? generator.Terrain.GetCellValueFast(x - 1, y + 2, z) : 0;
        var num13 = Terrain.ExtractContents(value11);
        var data11 = Terrain.ExtractData(value11);
        var num14 = TextureSlot;
        if ((num == 104 && HasFireOnFace(data2, 3)) || (num12 == 104 && HasFireOnFace(data10, 1)))
        {
            num14 += 16;
            if ((num2 == 104 && HasFireOnFace(data3, 3)) || (num13 == 104 && HasFireOnFace(data11, 1)))
            {
                num14 += 16;
            }
        }

        var vertices4 = alphaTestSubsetsByFace[3].Vertices;
        var indices4 = alphaTestSubsetsByFace[3].Indices;
        var count4 = vertices4.Count;
        vertices4.Count += 4;
        BlockGeometryGenerator.SetupLitCornerVertex(x, y, z, Color.White, num14, 0, ref vertices4.Array[count4]);
        BlockGeometryGenerator.SetupLitCornerVertex(x, y + 1, z, Color.White, num14, 3,
            ref vertices4.Array[count4 + 1]);
        BlockGeometryGenerator.SetupLitCornerVertex(x, y + 1, z + 1, Color.White, num14, 2,
            ref vertices4.Array[count4 + 2]);
        BlockGeometryGenerator.SetupLitCornerVertex(x, y, z + 1, Color.White, num14, 1,
            ref vertices4.Array[count4 + 3]);
        indices4.Add(count4);
        indices4.Add(count4 + 2);
        indices4.Add(count4 + 1);
        indices4.Add(count4 + 1);
        indices4.Add(count4 + 2);
        indices4.Add(count4);
        indices4.Add(count4 + 2);
        indices4.Add(count4);
        indices4.Add(count4 + 3);
        indices4.Add(count4 + 3);
        indices4.Add(count4);
        indices4.Add(count4 + 2);
    }

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData
    )
    {
    }

    public override bool ShouldAvoid(int value)
    {
        return true;
    }

    public static bool HasFireOnFace(int data, int face)
    {
        if (data == 0)
        {
            return true;
        }

        if ((data & (1 << face)) != 0)
        {
            return true;
        }

        return false;
    }
}
