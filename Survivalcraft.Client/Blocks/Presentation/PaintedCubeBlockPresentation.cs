using Engine.Graphics;

using Game.Modding.Blocks;
using Game.World.Presentation;

namespace Game.Blocks.Presentation;

public abstract class PaintedCubeBlockPresentation(
    PaintedCubeBlock block,
    BlockPresentationCatalog catalog,
    int coloredTextureSlot
) : CubeBlockPresentation(block, catalog)
{
    protected new PaintedCubeBlock Block => (PaintedCubeBlock)base.Block;

    public int ColoredTextureSlot = coloredTextureSlot;

    public override int GetFaceTextureSlot(int face, int value)
    {
        return !PaintedCubeBlock.IsColored(Terrain.ExtractData(value)) ? TextureSlot : ColoredTextureSlot;
    }

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
        var color = PalettePresentation.GetColor(generator.SubsystemPalette, PaintedCubeBlock.GetColor(data));
        generator.GenerateCubeVertices(this, value, x, y, z, color, geometry.OpaqueSubsetsByFace);
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
        color *= PalettePresentation.GetColor(environmentData.SubsystemTerrain, PaintedCubeBlock.GetColor(data));
        Catalog.DrawCubeBlock(
            primitivesRenderer,
            value,
            new Vector3(size),
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
        var color = PalettePresentation.GetColor(subsystemTerrain, PaintedCubeBlock.GetColor(data));
        return new BlockDebrisParticleSystem(
            subsystemTerrain,
            position,
            strength,
            DestructionDebrisScale,
            color,
            GetFaceTextureSlot(0, value)
        );
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var data = Terrain.ExtractData(value);
        return PalettePresentation.GetName(
            PaintedCubeBlock.GetColor(data),
            Localization.ClientLanguage.Catalog.GetBlock(
                $"{Block.GetType().Name}:{data.ToString()}",
                "DisplayName"
            )
        );
    }

    public override string GetCategory(int value)
    {
        return !PaintedCubeBlock.GetColor(Terrain.ExtractData(value)).HasValue ? base.GetCategory(value) : "Painted";
    }
}
