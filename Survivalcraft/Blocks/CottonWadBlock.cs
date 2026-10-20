using Engine.Graphics;

namespace Game.Blocks;

public class CottonWadBlock : FlatBlock
{
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
