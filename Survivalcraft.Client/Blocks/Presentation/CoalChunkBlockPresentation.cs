using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CoalChunkBlockPresentation(CoalChunkBlock block, BlockPresentationCatalog catalog)
    : ChunkBlockPresentation(
        block,
        catalog,
        Matrix.CreateRotationX(1f) * Matrix.CreateRotationZ(2f),
        Matrix.CreateTranslation(0.875f, 0.1875f, 0f),
        new Color(255, 255, 255),
        false
    )
{
    protected new CoalChunkBlock Block => (CoalChunkBlock)base.Block;
}
