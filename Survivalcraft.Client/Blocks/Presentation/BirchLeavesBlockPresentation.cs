using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class BirchLeavesBlockPresentation(BirchLeavesBlock block, BlockPresentationCatalog catalog)
    : DeciduousLeavesBlockPresentation(
        block,
        catalog,
        BlockColorsMap.BirchLeavesColorsMap,
        new Color(220, 170, 30),
        new Color(255, 230, 70),
        1.25f
    )
{
    protected new BirchLeavesBlock Block => (BirchLeavesBlock)base.Block;

    public override int GetFaceTextureSlot(int face, int value)
    {
        return DeciduousLeavesBlock.GetSeason(Terrain.ExtractData(value)) == Season.Winter
            ? 106
            : base.GetFaceTextureSlot(face, value);
    }
}
