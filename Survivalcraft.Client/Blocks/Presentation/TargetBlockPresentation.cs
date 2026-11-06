using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class TargetBlockPresentation(TargetBlock block, BlockPresentationCatalog catalog)
    : MountedElectricElementBlockPresentation(block, catalog)
{
    protected new TargetBlock Block => (TargetBlock)base.Block;

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        var subsetAlphaTest = geometry.SubsetAlphaTest;
        var vertices = subsetAlphaTest.Vertices;
        var indices = subsetAlphaTest.Indices;
        var count = vertices.Count;
        var data = Terrain.ExtractData(value);
        var num = Terrain.ExtractLight(value);
        var mountingFace = TargetBlock.GetMountingFace(data);
        var s = LightingManager.LightIntensityByLightValueAndFace[num + 16 * mountingFace];
        var color = Color.White * s;
        color.A = byte.MaxValue;
        switch (mountingFace)
        {
            case 2:
                vertices.Count += 4;
                BlockGeometryGenerator.SetupLitCornerVertex(
                    x,
                    y,
                    z + 1,
                    color,
                    TextureSlot,
                    0,
                    ref vertices.Array[count]
                );
                BlockGeometryGenerator.SetupLitCornerVertex(
                    x + 1,
                    y,
                    z + 1,
                    color,
                    TextureSlot,
                    1,
                    ref vertices.Array[count + 1]
                );
                BlockGeometryGenerator.SetupLitCornerVertex(
                    x + 1,
                    y + 1,
                    z + 1,
                    color,
                    TextureSlot,
                    2,
                    ref vertices.Array[count + 2]
                );
                BlockGeometryGenerator.SetupLitCornerVertex(
                    x,
                    y + 1,
                    z + 1,
                    color,
                    TextureSlot,
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
                break;
            case 3:
                vertices.Count += 4;
                BlockGeometryGenerator.SetupLitCornerVertex(
                    x + 1,
                    y,
                    z,
                    color,
                    TextureSlot,
                    0,
                    ref vertices.Array[count]
                );
                BlockGeometryGenerator.SetupLitCornerVertex(
                    x + 1,
                    y + 1,
                    z,
                    color,
                    TextureSlot,
                    3,
                    ref vertices.Array[count + 1]
                );
                BlockGeometryGenerator.SetupLitCornerVertex(
                    x + 1,
                    y + 1,
                    z + 1,
                    color,
                    TextureSlot,
                    2,
                    ref vertices.Array[count + 2]
                );
                BlockGeometryGenerator.SetupLitCornerVertex(
                    x + 1,
                    y,
                    z + 1,
                    color,
                    TextureSlot,
                    1,
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
                break;
            case 0:
                vertices.Count += 4;
                BlockGeometryGenerator.SetupLitCornerVertex(
                    x,
                    y,
                    z,
                    color,
                    TextureSlot,
                    0,
                    ref vertices.Array[count]
                );
                BlockGeometryGenerator.SetupLitCornerVertex(
                    x + 1,
                    y,
                    z,
                    color,
                    TextureSlot,
                    1,
                    ref vertices.Array[count + 1]
                );
                BlockGeometryGenerator.SetupLitCornerVertex(
                    x + 1,
                    y + 1,
                    z,
                    color,
                    TextureSlot,
                    2,
                    ref vertices.Array[count + 2]
                );
                BlockGeometryGenerator.SetupLitCornerVertex(
                    x,
                    y + 1,
                    z,
                    color,
                    TextureSlot,
                    3,
                    ref vertices.Array[count + 3]
                );
                indices.Add(count);
                indices.Add(count + 2);
                indices.Add(count + 1);
                indices.Add(count + 1);
                indices.Add(count + 2);
                indices.Add(count);
                indices.Add(count + 2);
                indices.Add(count);
                indices.Add(count + 3);
                indices.Add(count + 3);
                indices.Add(count);
                indices.Add(count + 2);
                break;
            case 1:
                vertices.Count += 4;
                BlockGeometryGenerator.SetupLitCornerVertex(
                    x,
                    y,
                    z,
                    color,
                    TextureSlot,
                    0,
                    ref vertices.Array[count]
                );
                BlockGeometryGenerator.SetupLitCornerVertex(
                    x,
                    y + 1,
                    z,
                    color,
                    TextureSlot,
                    3,
                    ref vertices.Array[count + 1]
                );
                BlockGeometryGenerator.SetupLitCornerVertex(
                    x,
                    y + 1,
                    z + 1,
                    color,
                    TextureSlot,
                    2,
                    ref vertices.Array[count + 2]
                );
                BlockGeometryGenerator.SetupLitCornerVertex(
                    x,
                    y,
                    z + 1,
                    color,
                    TextureSlot,
                    1,
                    ref vertices.Array[count + 3]
                );
                indices.Add(count);
                indices.Add(count + 2);
                indices.Add(count + 1);
                indices.Add(count + 1);
                indices.Add(count + 2);
                indices.Add(count);
                indices.Add(count + 2);
                indices.Add(count);
                indices.Add(count + 3);
                indices.Add(count + 3);
                indices.Add(count);
                indices.Add(count + 2);
                break;
        }
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
        Catalog.DrawFlatOrImageExtrusionBlock(
            primitivesRenderer,
            value,
            size,
            ref matrix,
            null,
            color,
            false,
            environmentData
        );
    }
}
