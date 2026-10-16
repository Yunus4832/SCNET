using Engine.Graphics;


namespace Game.Blocks;

public class FlourBlock : FlatBlock
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
            color * new Color(248, 255, 232),
            false,
            environmentData
        );
    }
}
