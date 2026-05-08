namespace Game.Blocks;

public class IronBlock : CubeBlock
{
    public const int Index = 46;

    public override bool FurnitureBuilt { get; set; } = true;

    public override void Initialize()
    {
        CraftingId = "ironblock";
    }
}
