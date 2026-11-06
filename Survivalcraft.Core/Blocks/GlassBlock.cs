namespace Game.Blocks;

public class GlassBlock : AlphaTestCubeBlock
{
    public bool CollapseSupportBlock = false;

    public override bool FurnitureBuilt { get; set; } = true;


    public override bool IsNonAttachable(int value) => false;

    public override bool IsCollapseSupportBlock(SubsystemTerrain subsystemTerrain, int value)
    {
        return CollapseSupportBlock;
    }
}
