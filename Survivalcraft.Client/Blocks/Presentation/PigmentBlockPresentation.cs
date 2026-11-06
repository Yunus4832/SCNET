using Engine.Graphics;

using Game.Modding.Blocks;
using Game.World.Presentation;

namespace Game.Blocks.Presentation;

public class PigmentBlockPresentation(PigmentBlock block, BlockPresentationCatalog catalog)
    : FlatBlockPresentation(block, catalog)
{
    protected new PigmentBlock Block => (PigmentBlock)base.Block;

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData
    )
    {
        var value2 = Terrain.ExtractData(value);
        Catalog.DrawFlatOrImageExtrusionBlock(
            primitivesRenderer,
            value,
            size,
            ref matrix,
            null,
            color * PalettePresentation.GetColor(environmentData.SubsystemTerrain, value2),
            false,
            environmentData
        );
    }
}
