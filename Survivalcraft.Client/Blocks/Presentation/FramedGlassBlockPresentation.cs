using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class FramedGlassBlockPresentation(FramedGlassBlock block, BlockPresentationCatalog catalog)
    : AlphaTestCubeBlockPresentation(block, catalog)
{
    protected new FramedGlassBlock Block => (FramedGlassBlock)base.Block;

    public override bool ShouldGenerateFace(
        SubsystemTerrain subsystemTerrain,
        int face,
        int value,
        int neighborValue,
        int x,
        int y,
        int z
    )
    {
        if (Terrain.ExtractContents(neighborValue) == Block.BlockIndex)
        {
            return false;
        }

        return base.ShouldGenerateFace(subsystemTerrain, face, value, neighborValue, x, y, z);
    }
}
