using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SaltpeterChunkBlockPresentation(SaltpeterChunkBlock block, BlockPresentationCatalog catalog)
    : ChunkBlockPresentation(
        block,
        catalog,
        Matrix.CreateRotationX(1f) * Matrix.CreateRotationZ(0f),
        Matrix.CreateTranslation(0.0625f, 0.4375f, 0f),
        new Color(255, 255, 255),
        false
    )
{
    protected new SaltpeterChunkBlock Block => (SaltpeterChunkBlock)base.Block;
}
