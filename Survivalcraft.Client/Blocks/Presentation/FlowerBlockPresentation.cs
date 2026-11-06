using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class FlowerBlockPresentation(FlowerBlock block, BlockPresentationCatalog catalog)
    : CrossBlockPresentation(block, catalog)
{
    protected new FlowerBlock Block => (FlowerBlock)base.Block;

    public override int GetFaceTextureSlot(int face, int value)
    {
        return !FlowerBlock.GetIsSmall(Terrain.ExtractData(value)) ? base.GetFaceTextureSlot(face, value) : 11;
    }

    public override int GetShadowStrength(int value)
    {
        if (!FlowerBlock.GetIsSmall(Terrain.ExtractData(value)))
        {
            return ShadowStrength;
        }

        return ShadowStrength / 2;
    }
}
