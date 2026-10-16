namespace Game.Blocks;

public class ClayBlock() : PaintedCubeBlock(15)
{
    public override bool FurnitureBuilt { get; set; } = true;
}
