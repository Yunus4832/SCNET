namespace Game.Blocks;

public class CrossbowBlock : Block
{
    public override int GetDamage(int value)
    {
        return (Terrain.ExtractData(value) >> 8) & 0xFF;
    }

    public override int SetDamage(int value, int damage)
    {
        var blockData = Terrain.ExtractData(value);
        blockData &= -65281;
        blockData |= MathUtils.Clamp(damage, 0, 255) << 8;
        return Terrain.ReplaceData(value, blockData);
    }


    public static ArrowBlock.ArrowType? GetArrowType(int data)
    {
        var encodedArrowType = (data >> 4) & 0xF;
        if (encodedArrowType != 0)
        {
            return (ArrowBlock.ArrowType)(encodedArrowType - 1);
        }

        return null;
    }

    public static int SetArrowType(int data, ArrowBlock.ArrowType? arrowType)
    {
        var encodedArrowType = (int)(arrowType.HasValue ? arrowType.Value + 1 : ArrowBlock.ArrowType.WoodenArrow);
        return (data & -241) | ((encodedArrowType & 0xF) << 4);
    }

    public static int GetDraw(int data)
    {
        return data & 0xF;
    }

    public static int SetDraw(int data, int draw)
    {
        return (data & -16) | (draw & 0xF);
    }
}
