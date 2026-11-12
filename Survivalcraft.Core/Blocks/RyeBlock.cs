namespace Game.Blocks;

public class RyeBlock : CrossBlock
{
    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, SetIsWild(SetSize(0, 7), true));
        yield return Terrain.MakeBlockValue(BlockIndex, SetIsWild(SetSize(0, 1), false));
        yield return Terrain.MakeBlockValue(BlockIndex, SetIsWild(SetSize(0, 3), false));
        yield return Terrain.MakeBlockValue(BlockIndex, SetIsWild(SetSize(0, 5), false));
        yield return Terrain.MakeBlockValue(BlockIndex, SetIsWild(SetSize(0, 7), false));
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
        var num = 0;
        var data = 0;
        var data2 = Terrain.ExtractData(oldValue);
        var size = GetSize(data2);
        var isWild = GetIsWild(data2);
        if (isWild)
        {
            num = size > 2 && Random.Float(0f, 1f) < 0.33f ? 1 : 0;
            data = 4;
        }
        else
        {
            switch (size)
            {
                case 5:
                    num = 1;
                    data = 4;
                    break;
                case 6:
                    num = Random.Int(1, 2);
                    data = 4;
                    break;
                case 7:
                    num = Random.Int(1, 3);
                    data = 5;
                    break;
            }
        }

        showDebris = true;
        BlockDropValue item;
        for (var i = 0; i < num; i++)
        {
            item = new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(173, data),
                Count = 1
            };
            dropValues.Add(item);
        }

        if (size != 7 || isWild || !Random.Bool(0.5f))
        {
            return;
        }

        item = new BlockDropValue
        {
            Value = 248,
            Count = 1
        };
        dropValues.Add(item);
    }

    public static int GetSize(int data)
    {
        return data & 7;
    }

    public static int SetSize(int data, int size)
    {
        size = MathUtils.Clamp(size, 0, 7);
        return (data & -8) | (size & 7);
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
