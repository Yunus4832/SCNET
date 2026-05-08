namespace Game.Blocks;

public class DiamondBlock : CubeBlock
{
    public const int Index = 126;

    public override bool FurnitureBuilt { get; set; } = true;

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = false;
        return false;
    }
}
