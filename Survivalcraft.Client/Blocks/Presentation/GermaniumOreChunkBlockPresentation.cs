using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class GermaniumOreChunkBlockPresentation(GermaniumOreChunkBlock block, BlockPresentationCatalog catalog)
    : ChunkBlockPresentation(
        block,
        catalog,
        Matrix.CreateRotationX(-1f) * Matrix.CreateRotationZ(1f),
        Matrix.CreateTranslation(0.0625f, 0.4375f, 0f),
        new Color(204, 181, 162),
        false
    )
{
    protected new GermaniumOreChunkBlock Block => (GermaniumOreChunkBlock)base.Block;
}
