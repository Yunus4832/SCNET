using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SulphurChunkBlockPresentation(SulphurChunkBlock block, BlockPresentationCatalog catalog)
    : ChunkBlockPresentation(
        block,
        catalog,
        Matrix.CreateRotationX(2f) * Matrix.CreateRotationZ(1f),
        Matrix.CreateTranslation(0.0625f, 0.4375f, 0f),
        new Color(255, 255, 140),
        true
    )
{
    protected new SulphurChunkBlock Block => (SulphurChunkBlock)base.Block;
}
