namespace Game.Blocks;

public class BowBlock : Block
{
    public override int GetDamage(int value)
    {
        return (Terrain.ExtractData(value) >> 8) & 0xFF;
    }

    public override int SetDamage(int value, int damage)
    {
        var num = Terrain.ExtractData(value);
        num &= -65281;
        num |= MathUtils.Clamp(damage, 0, 255) << 8;
        return Terrain.ReplaceData(value, num);
    }


    public static ArrowBlock.ArrowType? GetArrowType(int data)
    {
        var num = (data >> 4) & 0xF;
        if (num != 0)
        {
            return (ArrowBlock.ArrowType)(num - 1);
        }

        return null;
    }

    public static int SetArrowType(int data, ArrowBlock.ArrowType? arrowType)
    {
        var num = (int)(arrowType.HasValue ? arrowType.Value + 1 : ArrowBlock.ArrowType.WoodenArrow);
        return (data & -241) | ((num & 0xF) << 4);
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
