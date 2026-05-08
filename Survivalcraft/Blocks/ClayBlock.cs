namespace Game.Blocks;

public class ClayBlock() : PaintedCubeBlock(15)
{
    public const int Index = 72;

    public override bool FurnitureBuilt { get; set; } = true;
}
