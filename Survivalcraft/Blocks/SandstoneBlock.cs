namespace Game.Blocks;

public class SandstoneBlock() : PaintedCubeBlock(64)
{
    public override bool FurnitureBuilt { get; set; } = true;
}
