namespace Game.Blocks;

public class GlassBlock : AlphaTestCubeBlock
{
    public const int Index = 15;

    public bool CollapseSupportBlock = false;

    public override bool FurnitureBuilt { get; set; } = true;

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
        if (Terrain.ExtractContents(neighborValue) == BlockIndex)
        {
            return false;
        }

        return base.ShouldGenerateFace(
            subsystemTerrain,
            face,
            value,
            neighborValue,
            x,
            y,
            z
        );
    }

    public override bool IsNonAttachable(int value) => false;

    public override bool IsCollapseSupportBlock(SubsystemTerrain subsystemTerrain, int value)
    {
        return CollapseSupportBlock;
    }
}
