using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IvyBlockPresentation(IvyBlock block, BlockPresentationCatalog catalog) : BlockPresentation(block, catalog)
{
    protected new IvyBlock Block => (IvyBlock)base.Block;

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
        var face = IvyBlock.GetFace(data);
        var s = LightingManager.LightIntensityByLightValueAndFace[num + 16 * CellFace.OppositeFace(face)];
        var color = BlockColorsMap.IvyColorsMap.Lookup(generator.Terrain, x, y, z) * s;
        color.A = byte.MaxValue;
        switch (face)
        {
            case 0:
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
            case 1:
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
            case 2:
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
            case 3:
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
        color *= BlockColorsMap.IvyColorsMap.Lookup(environmentData.Temperature, environmentData.Humidity);
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

    public override BlockDebrisParticleSystem? CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        if (DestructionDebrisScale <= 0f) { return null; }

        var color = BlockColorsMap.IvyColorsMap.Lookup(
            subsystemTerrain.Terrain,
            Terrain.ToCell(position.X),
            Terrain.ToCell(position.Y),
            Terrain.ToCell(position.Z)
        );
        return new BlockDebrisParticleSystem(
            subsystemTerrain,
            position,
            strength,
            DestructionDebrisScale,
            color,
            TextureSlot
        );
    }
}
