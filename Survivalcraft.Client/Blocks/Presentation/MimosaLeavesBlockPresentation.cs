using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class MimosaLeavesBlockPresentation(MimosaLeavesBlock block, BlockPresentationCatalog catalog)
    : DeciduousLeavesBlockPresentation(
        block,
        catalog,
        BlockColorsMap.MimosaLeavesColorsMap,
        new Color(192, 100, 0),
        new Color(192, 150, 0),
        1.25f
    )
{
    protected new MimosaLeavesBlock Block => (MimosaLeavesBlock)base.Block;

    public override int GetFaceTextureSlot(int face, int value)
    {
        return DeciduousLeavesBlock.GetSeason(Terrain.ExtractData(value)) == Season.Winter
            ? 106
            : base.GetFaceTextureSlot(face, value);
    }
}
