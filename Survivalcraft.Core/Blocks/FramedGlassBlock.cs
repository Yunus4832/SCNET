namespace Game.Blocks;

public class FramedGlassBlock : AlphaTestCubeBlock
{
    public bool CollapseSupportBlock = false;


    public override bool IsNonAttachable(int value) => false;

    public override bool IsCollapseSupportBlock(SubsystemTerrain subsystemTerrain, int value)
    {
        return CollapseSupportBlock;
    }
}
