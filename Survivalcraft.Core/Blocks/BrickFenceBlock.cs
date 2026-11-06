namespace Game.Blocks;

public class BrickFenceBlock() : FenceBlock("Models/StoneFence")
{
    public override bool ShouldConnectTo(int value)
    {
        var num = Terrain.ExtractContents(value);
        return !Catalog.Blocks[num].Transparent || base.ShouldConnectTo(value);
    }
}
