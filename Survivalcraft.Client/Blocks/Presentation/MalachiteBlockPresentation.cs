using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class MalachiteBlockPresentation(MalachiteBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new MalachiteBlock Block => (MalachiteBlock)base.Block;
}
