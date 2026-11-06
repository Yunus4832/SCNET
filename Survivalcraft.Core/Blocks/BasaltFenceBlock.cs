namespace Game.Blocks;

public class BasaltFenceBlock() : FenceBlock("Models/StoneFence")
{
    public override bool ShouldConnectTo(int value)
    {
        var num = Terrain.ExtractContents(value);
        return !Catalog.Blocks[num].Transparent || base.ShouldConnectTo(value);
    }
}
