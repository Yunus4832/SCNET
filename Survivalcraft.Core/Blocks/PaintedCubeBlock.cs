namespace Game.Blocks;

public abstract class PaintedCubeBlock : CubeBlock, IPaintableBlock
{
    public int? GetPaintColor(int value)
    {
        return GetColor(Terrain.ExtractData(value));
    }

    public int Paint(SubsystemTerrain? terrain, int value, int? color)
    {
        var data = Terrain.ExtractData(value);
        return Terrain.ReplaceData(value, SetColor(data, color));
    }


    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, SetColor(0, null));
        var i = 0;
        while (i < 16)
        {
            yield return Terrain.MakeBlockValue(BlockIndex, SetColor(0, i));
            var num = i + 1;
            i = num;
        }
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
        if (GetColor(data).HasValue)
        {
            showDebris = true;
            if (toolLevel >= RequiredToolLevel)
            {
                dropValues.Add(
                    new BlockDropValue
                    {
                        Value = Terrain.MakeBlockValue(DropContent, data),
                        Count = (int)DropCount
                    }
                );
            }
        }
        else
        {
            base.GetDropValues(subsystemTerrain, oldValue, newValue, toolLevel, dropValues, out showDebris);
        }
    }


    public static bool IsColored(int data)
    {
        return (data & 1) != 0;
    }

    public static int? GetColor(int data)
    {
        if ((data & 1) != 0)
        {
            return (data >> 1) & 0xF;
        }

        return null;
    }

    public static int SetColor(int data, int? color)
    {
        if (color.HasValue)
        {
            return (data & -32) | 1 | (color.Value << 1);
        }

        return data & -32;
    }
}
