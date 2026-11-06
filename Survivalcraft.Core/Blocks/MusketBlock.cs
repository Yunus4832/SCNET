namespace Game.Blocks;

public class MusketBlock : Block
{
    public enum LoadState
    {
        Empty = 0,

        Gunpowder = 1,

        Wad = 2,

        Loaded = 3
    }


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

    public static LoadState GetLoadState(int data)
    {
        return (LoadState)(data & 3);
    }

    public static int SetLoadState(int data, LoadState loadState)
    {
        return (data & -4) | (int)(loadState & LoadState.Loaded);
    }

    public static bool GetHammerState(int data)
    {
        return (data & 4) != 0;
    }

    public static int SetHammerState(int data, bool state)
    {
        return (data & -5) | ((state ? 1 : 0) << 2);
    }

    public static BulletBlock.BulletType? GetBulletType(int data)
    {
        var num = (data >> 4) & 0xF;
        if (num != 0)
        {
            return (BulletBlock.BulletType)(num - 1);
        }

        return null;
    }

    public static int SetBulletType(int data, BulletBlock.BulletType? bulletType)
    {
        var num = (int)(bulletType.HasValue ? bulletType.Value + 1 : BulletBlock.BulletType.MusketBall);
        return (data & -241) | ((num & 0xF) << 4);
    }
}
