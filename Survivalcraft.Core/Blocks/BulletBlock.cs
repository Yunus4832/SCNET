namespace Game.Blocks;

public class BulletBlock : FlatBlock
{
    public enum BulletType
    {
        MusketBall = 0,

        Buckshot = 1,

        BuckshotBall = 2
    }

    public static readonly float[] Sizes =
    [
        1f,
        1f,
        0.33f
    ];


    public static readonly float[] WeaponPowers =
    [
        80f,
        0f,
        3.6f
    ];

    public static readonly float[] ExplosionPressures = new float[3];


    public override float GetProjectilePower(int value)
    {
        var bulletType = (int)GetBulletType(Terrain.ExtractData(value));
        if (bulletType < 0 || bulletType >= WeaponPowers.Length)
        {
            return 0f;
        }

        return WeaponPowers[bulletType];
    }

    public override float GetExplosionPressure(int value)
    {
        var bulletType = (int)GetBulletType(Terrain.ExtractData(value));
        if (bulletType < 0 || bulletType >= ExplosionPressures.Length)
        {
            return 0f;
        }

        return ExplosionPressures[bulletType];
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        return EnumUtils.GetEnumValues(typeof(BulletType)).Select(enumValue =>
            Terrain.MakeBlockValue(BlockIndex, 0, SetBulletType(0, (BulletType)enumValue))
        );
    }


    public static BulletType GetBulletType(int data)
    {
        return (BulletType)(data & 0xF);
    }

    public static int SetBulletType(int data, BulletType bulletType)
    {
        return (data & -16) | (int)(bulletType & (BulletType)15);
    }
}
