namespace Game.Blocks;

public class RottenPumpkinBlock() : BasePumpkinBlock(true)
{
    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = false;
        return false;
    }
}
