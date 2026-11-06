using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class ShadowBlockPresentation(ShadowBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new ShadowBlock Block => (ShadowBlock)base.Block;

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

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
    }

    public override int GetShadowStrength(int value)
    {
        return Terrain.ExtractData(value) - 128;
    }
}
