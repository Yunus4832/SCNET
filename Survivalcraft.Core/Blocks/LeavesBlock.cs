namespace Game.Blocks;

public abstract class LeavesBlock : AlphaTestCubeBlock
{
    public readonly Random SharedRandom = new();


    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        if (SharedRandom.Bool(0.15f))
        {
            dropValues.Add(
                new BlockDropValue
                {
                    Value = 23,
                    Count = 1
                }
            );
            showDebris = true;
        }
        else
        {
            base.GetDropValues(subsystemTerrain, oldValue, newValue, toolLevel, dropValues, out showDebris);
        }
    }
}
