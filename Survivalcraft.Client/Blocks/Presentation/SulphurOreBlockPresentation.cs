using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SulphurOreBlockPresentation(SulphurOreBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new SulphurOreBlock Block => (SulphurOreBlock)base.Block;
}
