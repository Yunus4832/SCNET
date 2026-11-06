using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SaltpeterOreBlockPresentation(SaltpeterOreBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new SaltpeterOreBlock Block => (SaltpeterOreBlock)base.Block;
}
