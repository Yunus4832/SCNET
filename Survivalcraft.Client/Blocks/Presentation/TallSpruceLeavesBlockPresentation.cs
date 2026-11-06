using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class TallSpruceLeavesBlockPresentation(TallSpruceLeavesBlock block, BlockPresentationCatalog catalog)
    : EvergreenLeavesBlockPresentation(block, catalog)
{
    protected new TallSpruceLeavesBlock Block => (TallSpruceLeavesBlock)base.Block;

    public override Color GetLeavesBlockColor(int value, Terrain terrain, int x, int y, int z)
    {
        return BlockColorsMap.TallSpruceLeavesColorsMap.Lookup(terrain, x, y, z);
    }

    public override Color GetLeavesItemColor(int value, DrawBlockEnvironmentData environmentData)
    {
        return BlockColorsMap.TallSpruceLeavesColorsMap.Lookup(environmentData);
    }
}
