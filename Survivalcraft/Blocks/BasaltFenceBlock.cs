namespace Game.Blocks;

public class BasaltFenceBlock() : FenceBlock(
    "Models/StoneFence",
    false,
    false,
    40,
    new Color(212, 212, 212),
    Color.White
)
{
    public const int Index = 163;

    public override bool ShouldConnectTo(int value)
    {
        var num = Terrain.ExtractContents(value);
        return !BlocksManager.Blocks[num].Transparent || base.ShouldConnectTo(value);
    }
}
