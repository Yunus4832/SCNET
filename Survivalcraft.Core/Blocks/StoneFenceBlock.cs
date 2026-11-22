namespace Game.Blocks;

public class StoneFenceBlock() : FenceBlock("Models/StoneFence")
{
    public override bool ShouldConnectTo(int value)
    {
        var neighborContents = Terrain.ExtractContents(value);
        return !Catalog.Blocks[neighborContents].Transparent || base.ShouldConnectTo(value);
    }
}
