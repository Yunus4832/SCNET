using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class FireBlockPresentation(FireBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new FireBlock Block => (FireBlock)base.Block;

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
        var contentsAbove = Terrain.ExtractContents(value2);
        var data2 = Terrain.ExtractData(value2);
        var value3 = y + 2 < 256 ? generator.Terrain.GetCellValueFast(x, y + 2, z) : 0;
        var contentsTwoAbove = Terrain.ExtractContents(value3);
        var data3 = Terrain.ExtractData(value3);
        if (FireBlock.HasFireOnFace(data, 0))
        {
            var value4 = y + 1 < 256 ? generator.Terrain.GetCellValueFast(x, y + 1, z + 1) : 0;
            var sideContentsAbove = Terrain.ExtractContents(value4);
            var data4 = Terrain.ExtractData(value4);
            var value5 = y + 2 < 256 ? generator.Terrain.GetCellValueFast(x, y + 2, z + 1) : 0;
            var sideContentsTwoAbove = Terrain.ExtractContents(value5);
            var data5 = Terrain.ExtractData(value5);
            var sideTextureSlot = TextureSlot;
            if ((contentsAbove == 104 && FireBlock.HasFireOnFace(data2, 0)) ||
                (sideContentsAbove == 104 && FireBlock.HasFireOnFace(data4, 2)))
            {
                sideTextureSlot += 16;
                if ((contentsTwoAbove == 104 && FireBlock.HasFireOnFace(data3, 0)) ||
                    (sideContentsTwoAbove == 104 && FireBlock.HasFireOnFace(data5, 2)))
                {
                    sideTextureSlot += 16;
                }
            }

            var vertices = alphaTestSubsetsByFace[0].Vertices;
            var indices = alphaTestSubsetsByFace[0].Indices;
            var count = vertices.Count;
            vertices.Count += 4;
            BlockGeometryGenerator.SetupLitCornerVertex(
                x,
                y,
                z + 1,
                Color.White,
                sideTextureSlot,
                0,
                ref vertices.Array[count]
            );
            BlockGeometryGenerator.SetupLitCornerVertex(
                x + 1,
                y,
                z + 1,
                Color.White,
                sideTextureSlot,
                1,
                ref vertices.Array[count + 1]
            );
            BlockGeometryGenerator.SetupLitCornerVertex(
                x + 1,
                y + 1,
                z + 1,
                Color.White,
                sideTextureSlot,
                2,
                ref vertices.Array[count + 2]
            );
            BlockGeometryGenerator.SetupLitCornerVertex(
                x,
                y + 1,
                z + 1,
                Color.White,
                sideTextureSlot,
                3,
                ref vertices.Array[count + 3]
            );
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

        if (FireBlock.HasFireOnFace(data, 1))
        {
            var value6 = y + 1 < 256 ? generator.Terrain.GetCellValueFast(x + 1, y + 1, z) : 0;
            var sideContentsAbove = Terrain.ExtractContents(value6);
            var data6 = Terrain.ExtractData(value6);
            var value7 = y + 2 < 256 ? generator.Terrain.GetCellValueFast(x + 1, y + 2, z) : 0;
            var sideContentsTwoAbove = Terrain.ExtractContents(value7);
            var data7 = Terrain.ExtractData(value7);
            var sideTextureSlot = TextureSlot;
            if ((contentsAbove == 104 && FireBlock.HasFireOnFace(data2, 1)) ||
                (sideContentsAbove == 104 && FireBlock.HasFireOnFace(data6, 3)))
            {
                sideTextureSlot += 16;
                if ((contentsTwoAbove == 104 && FireBlock.HasFireOnFace(data3, 1)) ||
                    (sideContentsTwoAbove == 104 && FireBlock.HasFireOnFace(data7, 3)))
                {
                    sideTextureSlot += 16;
                }
            }

            var vertices2 = alphaTestSubsetsByFace[1].Vertices;
            var indices2 = alphaTestSubsetsByFace[1].Indices;
            var count2 = vertices2.Count;
            vertices2.Count += 4;
            BlockGeometryGenerator.SetupLitCornerVertex(
                x + 1,
                y,
                z,
                Color.White,
                sideTextureSlot,
                0,
                ref vertices2.Array[count2]
            );
            BlockGeometryGenerator.SetupLitCornerVertex(
                x + 1,
                y + 1,
                z,
                Color.White,
                sideTextureSlot,
                3,
                ref vertices2.Array[count2 + 1]
            );
            BlockGeometryGenerator.SetupLitCornerVertex(
                x + 1,
                y + 1,
                z + 1,
                Color.White,
                sideTextureSlot,
                2,
                ref vertices2.Array[count2 + 2]
            );
            BlockGeometryGenerator.SetupLitCornerVertex(
                x + 1,
                y,
                z + 1,
                Color.White,
                sideTextureSlot,
                1,
                ref vertices2.Array[count2 + 3]
            );
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

        if (FireBlock.HasFireOnFace(data, 2))
        {
            var value8 = y + 1 < 256 ? generator.Terrain.GetCellValueFast(x, y + 1, z - 1) : 0;
            var sideContentsAbove = Terrain.ExtractContents(value8);
            var data8 = Terrain.ExtractData(value8);
            var value9 = y + 2 < 256 ? generator.Terrain.GetCellValueFast(x, y + 2, z - 1) : 0;
            var sideContentsTwoAbove = Terrain.ExtractContents(value9);
            var data9 = Terrain.ExtractData(value9);
            var sideTextureSlot = TextureSlot;
            if ((contentsAbove == 104 && FireBlock.HasFireOnFace(data2, 2)) ||
                (sideContentsAbove == 104 && FireBlock.HasFireOnFace(data8, 0)))
            {
                sideTextureSlot += 16;
                if ((contentsTwoAbove == 104 && FireBlock.HasFireOnFace(data3, 2)) ||
                    (sideContentsTwoAbove == 104 && FireBlock.HasFireOnFace(data9, 0)))
                {
                    sideTextureSlot += 16;
                }
            }

            var vertices3 = alphaTestSubsetsByFace[2].Vertices;
            var indices3 = alphaTestSubsetsByFace[2].Indices;
            var count3 = vertices3.Count;
            vertices3.Count += 4;
            BlockGeometryGenerator.SetupLitCornerVertex(
                x,
                y,
                z,
                Color.White,
                sideTextureSlot,
                0,
                ref vertices3.Array[count3]
            );
            BlockGeometryGenerator.SetupLitCornerVertex(
                x + 1,
                y,
                z,
                Color.White,
                sideTextureSlot,
                1,
                ref vertices3.Array[count3 + 1]
            );
            BlockGeometryGenerator.SetupLitCornerVertex(
                x + 1,
                y + 1,
                z,
                Color.White,
                sideTextureSlot,
                2,
                ref vertices3.Array[count3 + 2]
            );
            BlockGeometryGenerator.SetupLitCornerVertex(
                x,
                y + 1,
                z,
                Color.White,
                sideTextureSlot,
                3,
                ref vertices3.Array[count3 + 3]
            );
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

        if (!FireBlock.HasFireOnFace(data, 3))
        {
            return;
        }

        var value10 = y + 1 < 256 ? generator.Terrain.GetCellValueFast(x - 1, y + 1, z) : 0;
        var endContentsAbove = Terrain.ExtractContents(value10);
        var data10 = Terrain.ExtractData(value10);
        var value11 = y + 2 < 256 ? generator.Terrain.GetCellValueFast(x - 1, y + 2, z) : 0;
        var endContentsTwoAbove = Terrain.ExtractContents(value11);
        var data11 = Terrain.ExtractData(value11);
        var endTextureSlot = TextureSlot;
        if ((contentsAbove == 104 && FireBlock.HasFireOnFace(data2, 3)) ||
            (endContentsAbove == 104 && FireBlock.HasFireOnFace(data10, 1)))
        {
            endTextureSlot += 16;
            if ((contentsTwoAbove == 104 && FireBlock.HasFireOnFace(data3, 3)) ||
                (endContentsTwoAbove == 104 && FireBlock.HasFireOnFace(data11, 1)))
            {
                endTextureSlot += 16;
            }
        }

        var vertices4 = alphaTestSubsetsByFace[3].Vertices;
        var indices4 = alphaTestSubsetsByFace[3].Indices;
        var count4 = vertices4.Count;
        vertices4.Count += 4;
        BlockGeometryGenerator.SetupLitCornerVertex(
            x,
            y,
            z,
            Color.White,
            endTextureSlot,
            0,
            ref vertices4.Array[count4]
        );
        BlockGeometryGenerator.SetupLitCornerVertex(
            x,
            y + 1,
            z,
            Color.White,
            endTextureSlot,
            3,
            ref vertices4.Array[count4 + 1]
        );
        BlockGeometryGenerator.SetupLitCornerVertex(
            x,
            y + 1,
            z + 1,
            Color.White,
            endTextureSlot,
            2,
            ref vertices4.Array[count4 + 2]
        );
        BlockGeometryGenerator.SetupLitCornerVertex(
            x,
            y,
            z + 1,
            Color.White,
            endTextureSlot,
            1,
            ref vertices4.Array[count4 + 3]
        );
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
}
