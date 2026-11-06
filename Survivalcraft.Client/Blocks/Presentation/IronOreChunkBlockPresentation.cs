using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IronOreChunkBlockPresentation(IronOreChunkBlock block, BlockPresentationCatalog catalog)
    : ChunkBlockPresentation(
        block,
        catalog,
        Matrix.CreateRotationX(0f) * Matrix.CreateRotationZ(2f),
        Matrix.CreateTranslation(0.9375f, 0.1875f, 0f),
        new Color(136, 74, 36),
        false
    )
{
    protected new IronOreChunkBlock Block => (IronOreChunkBlock)base.Block;
}
