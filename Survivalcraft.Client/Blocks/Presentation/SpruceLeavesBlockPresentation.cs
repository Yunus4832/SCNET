using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SpruceLeavesBlockPresentation(SpruceLeavesBlock block, BlockPresentationCatalog catalog)
    : EvergreenLeavesBlockPresentation(block, catalog)
{
    protected new SpruceLeavesBlock Block => (SpruceLeavesBlock)base.Block;

    public override Color GetLeavesBlockColor(int value, Terrain terrain, int x, int y, int z)
    {
        return BlockColorsMap.SpruceLeavesColorsMap.Lookup(terrain, x, y, z);
    }

    public override Color GetLeavesItemColor(int value, DrawBlockEnvironmentData environmentData)
    {
        return BlockColorsMap.SpruceLeavesColorsMap.Lookup(environmentData);
    }
}
