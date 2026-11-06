using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class BedrockBlockPresentation(BedrockBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new BedrockBlock Block => (BedrockBlock)base.Block;
}
