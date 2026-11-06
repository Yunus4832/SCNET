using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class GunpowderBlockPresentation(GunpowderBlock block, BlockPresentationCatalog catalog)
    : ChunkBlockPresentation(
        block,
        catalog,
        Matrix.CreateScale(0.75f) * Matrix.CreateRotationX(4f) * Matrix.CreateRotationZ(3f),
        Matrix.CreateScale(1f) * Matrix.CreateTranslation(0.0625f, 0.875f, 0f),
        new Color(255, 255, 255),
        false
    )
{
    protected new GunpowderBlock Block => (GunpowderBlock)base.Block;
}
