using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class SignBlockPresentation(SignBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new SignBlock Block => (SignBlock)base.Block;


    public abstract BlockMesh GetSignSurfaceBlockMesh(int data);
}
