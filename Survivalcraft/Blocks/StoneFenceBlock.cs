namespace Game.Blocks;

public class StoneFenceBlock() : FenceBlock(
    "Models/StoneFence",
    false,
    false,
    24,
    new Color(212, 212, 212),
    Color.White
)
{
    public const int Index = 202;

    public override bool ShouldConnectTo(int value)
    {
        var num = Terrain.ExtractContents(value);
        return !BlocksManager.Blocks[num].Transparent || base.ShouldConnectTo(value);
    }
}
