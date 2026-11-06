using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class MalachiteChunkBlockPresentation(MalachiteChunkBlock block, BlockPresentationCatalog catalog)
    : ChunkBlockPresentation(
        block,
        catalog,
        Matrix.CreateRotationX(2f) * Matrix.CreateRotationZ(3f),
        Matrix.CreateTranslation(0.1875f, 0.6875f, 0f),
        new Color(255, 255, 255),
        false
    )
{
    protected new MalachiteChunkBlock Block => (MalachiteChunkBlock)base.Block;
}
