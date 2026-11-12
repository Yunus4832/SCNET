namespace Game.Blocks;

public class CottonBlock : CrossBlock
{
    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, SetIsWild(SetSize(0, 2), true));
        yield return Terrain.MakeBlockValue(BlockIndex, SetIsWild(SetSize(0, 1), false));
        yield return Terrain.MakeBlockValue(BlockIndex, SetIsWild(SetSize(0, 2), false));
    }


    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        var data = Terrain.ExtractData(oldValue);
        if (GetSize(data) == 2)
        {
            var item = new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(173, 6),
                Count = Random.Int(1, 2)
            };
            dropValues.Add(item);
            if (!GetIsWild(data))
            {
                var num = Random.Int(1, 2);
                for (var i = 0; i < num; i++)
                {
                    item = new BlockDropValue
                    {
                        Value = Terrain.MakeBlockValue(205, 0),
                        Count = 1
                    };
                    dropValues.Add(item);
                }

                if (Random.Bool(0.5f))
                {
                    item = new BlockDropValue
                    {
                        Value = Terrain.MakeBlockValue(248),
                        Count = 1
                    };
                    dropValues.Add(item);
                }
            }
        }

        showDebris = true;
    }


    public static int GetSize(int data)
    {
        return data & 3;
    }

    public static int SetSize(int data, int size)
    {
        size = MathUtils.Clamp(size, 0, 2);
        return (data & -4) | (size & 3);
    }

    public static bool GetIsWild(int data)
    {
        return (data & 8) != 0;
    }

    public static int SetIsWild(int data, bool isWild)
    {
        if (!isWild)
        {
            return data & -9;
        }

        return data | 8;
    }
}
