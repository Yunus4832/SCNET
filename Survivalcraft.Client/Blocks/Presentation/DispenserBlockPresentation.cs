using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class DispenserBlockPresentation(DispenserBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new DispenserBlock Block => (DispenserBlock)base.Block;

    public override int GetFaceTextureSlot(int face, int value)
    {
        var direction = DispenserBlock.GetDirection(Terrain.ExtractData(value));
        return face != direction ? 42 : 59;
    }
}
