namespace Game.Blocks;

public class PumpkinBlock() : BasePumpkinBlock(false)
{
    public const int Index = 131;

    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        base.GetDropValues(subsystemTerrain, oldValue, newValue, toolLevel, dropValues, out showDebris);
        var data = Terrain.ExtractData(oldValue);
        if (GetSize(data) == 7 && !GetIsDead(data) && Random.Bool(0.5f))
        {
            dropValues.Add(new BlockDropValue
            {
                Value = 248,
                Count = 1
            });
        }
    }

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = false;
        return false;
    }
}
