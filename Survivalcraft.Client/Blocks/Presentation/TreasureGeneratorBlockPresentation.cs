using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class TreasureGeneratorBlockPresentation(TreasureGeneratorBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new TreasureGeneratorBlock Block => (TreasureGeneratorBlock)base.Block;

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
