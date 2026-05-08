namespace Game.Blocks;

public class SandstoneBlock() : PaintedCubeBlock(64)
{
    public const int Index = 4;

    public override bool FurnitureBuilt { get; set; } = true;
}
