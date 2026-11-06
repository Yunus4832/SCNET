using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class PoplarLeavesBlockPresentation(PoplarLeavesBlock block, BlockPresentationCatalog catalog)
    : DeciduousLeavesBlockPresentation(
        block,
        catalog,
        BlockColorsMap.PoplarLeavesColorsMap,
        new Color(220, 130, 20),
        new Color(255, 190, 60),
        1.5f
    )
{
    protected new PoplarLeavesBlock Block => (PoplarLeavesBlock)base.Block;

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
