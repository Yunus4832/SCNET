namespace Game.Blocks;

public class IronFenceBlock() : FenceBlock(
    "Models/IronFence",
    true,
    true,
    58,
    new Color(192, 192, 192),
    new Color(80, 80, 80)
)
{
    public const int Index = 193;
}
