using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class StoneChunkBlockPresentation(StoneChunkBlock block, BlockPresentationCatalog catalog)
    : ChunkBlockPresentation(
        block,
        catalog,
        Matrix.CreateScale(0.75f) * Matrix.CreateRotationX(0f) * Matrix.CreateRotationZ(1f),
        Matrix.CreateScale(0.75f) * Matrix.CreateTranslation(0.1875f, 0.0625f, 0f),
        new Color(255, 255, 255),
        true
    )
{
    protected new StoneChunkBlock Block => (StoneChunkBlock)base.Block;
}
