namespace Game.Blocks;

public abstract class FlowerBlock : CrossBlock
{
    public override int GetFaceTextureSlot(int face, int value)
    {
        return !GetIsSmall(Terrain.ExtractData(value)) ? base.GetFaceTextureSlot(face, value) : 11;
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
        if (!GetIsSmall(data))
        {
            dropValues.Add(new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(Terrain.ExtractContents(oldValue), 0, data),
                Count = 1
            });
        }

        showDebris = true;
    }

    public override int GetShadowStrength(int value)
    {
        if (!GetIsSmall(Terrain.ExtractData(value)))
        {
            return ShadowStrength;
        }

        return ShadowStrength / 2;
    }

    public static bool GetIsSmall(int data)
    {
        return (data & 1) != 0;
    }

    public static int SetIsSmall(int data, bool isSmall)
    {
        if (!isSmall)
        {
            return data & -2;
        }

        return data | 1;
    }
}
