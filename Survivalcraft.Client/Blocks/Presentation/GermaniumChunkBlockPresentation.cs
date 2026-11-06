using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class GermaniumChunkBlockPresentation(GermaniumChunkBlock block, BlockPresentationCatalog catalog)
    : ChunkBlockPresentation(
        block,
        catalog,
        Matrix.CreateRotationX(3f) * Matrix.CreateRotationZ(2f),
        Matrix.CreateTranslation(0.875f, 0.25f, 0f),
        new Color(255, 255, 255),
        false
    )
{
    protected new GermaniumChunkBlock Block => (GermaniumChunkBlock)base.Block;
}
