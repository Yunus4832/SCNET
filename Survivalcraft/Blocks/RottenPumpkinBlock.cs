namespace Game.Blocks;

public class RottenPumpkinBlock() : BasePumpkinBlock(true)
{
    public const int Index = 244;

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = false;
        return false;
    }
}
