using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class WireThroughBlockPresentation(
    WireThroughBlock block,
    BlockPresentationCatalog catalog,
    int wiredTextureSlot,
    int unwiredTextureSlot
) : CubeBlockPresentation(block, catalog)
{
    protected new WireThroughBlock Block => (WireThroughBlock)base.Block;

    public override int GetFaceTextureSlot(int face, int value)
    {
        var wiredFace = WireThroughBlock.GetWiredFace(Terrain.ExtractData(value));
        if (wiredFace == face || CellFace.OppositeFace(wiredFace) == face)
        {
            return WiredTextureSlot;
        }

        return UnwiredTextureSlot;
    }

    public int UnwiredTextureSlot = unwiredTextureSlot;


    public int WiredTextureSlot = wiredTextureSlot;
}
