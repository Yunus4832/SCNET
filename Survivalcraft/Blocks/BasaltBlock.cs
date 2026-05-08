namespace Game.Blocks;

public class BasaltBlock() : PaintedCubeBlock(40)
{
    public override bool FurnitureBuilt { get; set; } = true;

    public const int Index = 67;
}
