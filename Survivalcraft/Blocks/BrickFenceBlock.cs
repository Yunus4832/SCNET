namespace Game.Blocks;

public class BrickFenceBlock() : FenceBlock(
    "Models/StoneFence",
    false,
    false,
    39,
    new Color(212, 212, 212),
    Color.White
)
{
    public override bool ShouldConnectTo(int value)
    {
        var num = Terrain.ExtractContents(value);
        return !Catalog.Blocks[num].Transparent || base.ShouldConnectTo(value);
    }
}
