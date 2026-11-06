namespace Game.Blocks;

public class FireBlock : Block
{
    public override bool ShouldAvoid(int value)
    {
        return true;
    }

    public static bool HasFireOnFace(int data, int face)
    {
        if (data == 0)
        {
            return true;
        }

        if ((data & (1 << face)) != 0)
        {
            return true;
        }

        return false;
    }
}
