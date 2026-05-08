namespace Game.Blocks;

public class MarbleBlock() : PaintedCubeBlock(51)
{
    public const int Index = 68;

    public override bool FurnitureBuilt { get; set; } = true;
}
