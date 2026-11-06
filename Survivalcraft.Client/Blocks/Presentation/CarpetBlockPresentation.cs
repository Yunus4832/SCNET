using Engine.Graphics;

using Game.Modding.Blocks;
using Game.World.Presentation;

namespace Game.Blocks.Presentation;

public class CarpetBlockPresentation(CarpetBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new CarpetBlock Block => (CarpetBlock)base.Block;

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        var data = Terrain.ExtractData(value);
        var fabricColor = PalettePresentation.GetFabricColor(generator.SubsystemPalette, CarpetBlock.GetColor(data));
        generator.GenerateCubeVertices(
            this,
            value,
            x,
            y,
            z,
            0.0625f,
            0.0625f,
            0.0625f,
            0.0625f,
            fabricColor,
            fabricColor,
            fabricColor,
            fabricColor,
            fabricColor,
            -1,
            geometry.OpaqueSubsetsByFace
        );
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
        var data = Terrain.ExtractData(value);
        color *= PalettePresentation.GetFabricColor(environmentData.SubsystemTerrain, CarpetBlock.GetColor(data));
        Catalog.DrawCubeBlock(
            primitivesRenderer,
            value,
            new Vector3(size, 0.0625f * size, size),
            ref matrix,
            color,
            color,
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

        var data = Terrain.ExtractData(value);
        var fabricColor = PalettePresentation.GetFabricColor(subsystemTerrain, CarpetBlock.GetColor(data));
        return new BlockDebrisParticleSystem(
            subsystemTerrain,
            position,
            strength,
            DestructionDebrisScale,
            fabricColor,
            TextureSlot
        );
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var value2 = Terrain.ExtractData(value);
        return PalettePresentation.GetName(
            CarpetBlock.GetColor(value2),
            Localization.ClientLanguage.Catalog.GetBlock("CarpetBlock:0", "DisplayName")
        );
    }
}
