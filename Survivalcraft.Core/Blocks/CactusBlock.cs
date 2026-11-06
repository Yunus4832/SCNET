namespace Game.Blocks;

public class CactusBlock : Block
{
    public override bool ShouldAvoid(int value)
    {
        return true;
    }

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = false;
        return false;
    }
}
