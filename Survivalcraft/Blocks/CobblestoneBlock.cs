namespace Game.Blocks;

public class CobblestoneBlock() : PaintedCubeBlock(69)
{
    public const int Index = 5;

    public override bool FurnitureBuilt { get; set; } = true;
}
