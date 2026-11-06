namespace Game.Blocks;

public class TallGrassBlock : CrossBlock
{
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
        if (!GetIsSmall(data))
        {
            dropValues.Add(
                new BlockDropValue
                {
                    Value = Terrain.MakeBlockValue(BlockIndex, 0, data),
                    Count = 1
                }
            );
        }

        showDebris = true;
    }


    public static bool GetIsSmall(int data)
    {
        return (data & 8) != 0;
    }

    public static int SetIsSmall(int data, bool isSmall)
    {
        if (!isSmall)
        {
            return data & -9;
        }

        return data | 8;
    }
}
