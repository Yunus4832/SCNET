namespace Game.Subsystems;

public class SubsystemInWaterBlockBehavior : SubsystemWaterBlockBehavior
{
    public override int[] HandledBlocks => [];

    public override void OnItemHarvested(int x, int y, int z, int blockValue, ref BlockDropValue dropValue,
        ref int newBlockValue)
    {
        var level = FluidBlock.GetLevel(Terrain.ExtractData(blockValue));
        newBlockValue = Terrain.MakeBlockValue(18, 0, FluidBlock.SetLevel(0, level));
        dropValue.Value = Terrain.MakeBlockValue(Terrain.ExtractContents(blockValue));
        dropValue.Count = 1;
    }
}
