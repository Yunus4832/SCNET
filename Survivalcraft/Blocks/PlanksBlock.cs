namespace Game.Blocks;

public class PlanksBlock() : PaintedCubeBlock(23)
{
    public const int Index = 21;

    public override bool FurnitureBuilt { get; set; } = true;
}
