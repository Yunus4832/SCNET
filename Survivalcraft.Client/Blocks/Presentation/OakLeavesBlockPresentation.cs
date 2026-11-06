using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class OakLeavesBlockPresentation(OakLeavesBlock block, BlockPresentationCatalog catalog)
    : DeciduousLeavesBlockPresentation(
        block,
        catalog,
        BlockColorsMap.OakLeavesColorsMap,
        new Color(230, 80, 0),
        new Color(255, 130, 20),
        2f
    )
{
    protected new OakLeavesBlock Block => (OakLeavesBlock)base.Block;

    public override int GetFaceTextureSlot(int face, int value)
    {
        var season = DeciduousLeavesBlock.GetSeason(Terrain.ExtractData(value));
        return season switch
        {
            Season.Winter => 106,
            Season.Spring => 107,
            _ => base.GetFaceTextureSlot(face, value)
        };
    }
}
