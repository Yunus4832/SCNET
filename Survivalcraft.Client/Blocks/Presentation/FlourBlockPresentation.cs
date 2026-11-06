using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class FlourBlockPresentation(FlourBlock block, BlockPresentationCatalog catalog)
    : FlatBlockPresentation(block, catalog)
{
    protected new FlourBlock Block => (FlourBlock)base.Block;

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
            color * new Color(248, 255, 232),
            false,
            environmentData
        );
    }
}
