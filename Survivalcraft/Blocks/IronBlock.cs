namespace Game.Blocks;

public class IronBlock : CubeBlock
{
    public override bool FurnitureBuilt { get; set; } = true;

    public override void Initialize()
    {
        CraftingId = "ironblock";
    }
}
