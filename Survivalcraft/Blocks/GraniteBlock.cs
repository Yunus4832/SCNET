namespace Game.Blocks;

public class GraniteBlock() : PaintedCubeBlock(24)
{
    public const int Index = 3;

    public override bool FurnitureBuilt { get; set; } = true;
}
