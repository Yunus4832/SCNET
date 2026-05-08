namespace Game.Blocks;

public class StoneBrickBlock() : PaintedCubeBlock(50)
{
    public const int Index = 26;

    public override bool FurnitureBuilt { get; set; } = true;
}
