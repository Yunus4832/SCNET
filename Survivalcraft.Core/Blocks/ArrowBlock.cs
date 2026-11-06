namespace Game.Blocks;

public class ArrowBlock : Block
{
    public enum ArrowType
    {
        WoodenArrow = 0,

        StoneArrow = 1,

        IronArrow = 2,

        DiamondArrow = 3,

        FireArrow = 4,

        IronBolt = 5,

        DiamondBolt = 6,

        ExplosiveBolt = 7,

        CopperArrow = 8
    }

    public static int[] Order = [0, 1, 8, 2, 3, 4, 5, 6, 7];

    public static string[] TipNames =
    [
        "ArrowTip",
        "ArrowTip",
        "ArrowTip",
        "ArrowTip",
        "ArrowFireTip",
        "BoltTip",
        "BoltTip",
        "BoltExplosiveTip",
        "ArrowTip"
    ];


    public static string[] ShaftNames =
    [
        "ArrowShaft",
        "ArrowShaft",
        "ArrowShaft",
        "ArrowShaft",
        "ArrowShaft",
        "BoltShaft",
        "BoltShaft",
        "BoltShaft",
        "ArrowShaft"
    ];


    public static string[] StabilizerNames =
    [
        "ArrowStabilizer",
        "ArrowStabilizer",
        "ArrowStabilizer",
        "ArrowStabilizer",
        "ArrowStabilizer",
        "BoltStabilizer",
        "BoltStabilizer",
        "BoltStabilizer",
        "ArrowStabilizer"
    ];


    public static string[] DisplayNames =
    [
        "木尖箭头",
        "石尖箭头",
        "铁尖箭头",
        "钻石尖箭头",
        "火尖箭头",
        "铁螺栓",
        "钻石尖螺栓",
        "爆炸螺栓",
        "铜尖箭头"
    ];

    public static float[] Offsets = [-0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.3f, -0.3f, -0.3f, -0.5f];

    public static float[] WeaponPowers = [5f, 7f, 14f, 18f, 4f, 28f, 36f, 8f, 10f];

    public static float[] IconViewScales = [0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 1.1f, 1.1f, 1.1f, 0.8f];

    public static float[] ExplosionPressures = [0f, 0f, 0f, 0f, 0f, 0f, 0f, 40f, 0f];


    public override float GetProjectilePower(int value)
    {
        var arrowType = (int)GetArrowType(Terrain.ExtractData(value));
        if (arrowType < 0 || arrowType >= WeaponPowers.Length)
        {
            return 0f;
        }

        return WeaponPowers[arrowType];
    }

    public override float GetExplosionPressure(int value)
    {
        var arrowType = (int)GetArrowType(Terrain.ExtractData(value));
        if (arrowType < 0 || arrowType >= ExplosionPressures.Length)
        {
            return 0f;
        }

        return ExplosionPressures[arrowType];
    }


    public override IEnumerable<int> GetCreativeValues()
    {
        var i = 0;
        while (i < Order.Length)
        {
            yield return Terrain.MakeBlockValue(BlockIndex, 0, SetArrowType(0, (ArrowType)Order[i]));
            var num = i + 1;
            i = num;
        }
    }


    public static ArrowType GetArrowType(int data)
    {
        return (ArrowType)(data & 0xF);
    }

    public static int SetArrowType(int data, ArrowType arrowType)
    {
        return (data & -16) | (int)(arrowType & (ArrowType)15);
    }
}
