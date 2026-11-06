namespace Game.Blocks;

public class WaterBlock() : FluidBlock(WaterMaxLevel)
{
    public const int WaterMaxLevel = 7;

    public override bool FurnitureBuilt { get; set; } = true;
}
