using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WickerLampBlockPresentation(WickerLampBlock block, BlockPresentationCatalog catalog)
    : AlphaTestCubeBlockPresentation(block, catalog)
{
    protected new WickerLampBlock Block => (WickerLampBlock)base.Block;

    public override int GetFaceTextureSlot(int face, int value)
    {
        return face != 5 ? TextureSlot : 4;
    }
}
