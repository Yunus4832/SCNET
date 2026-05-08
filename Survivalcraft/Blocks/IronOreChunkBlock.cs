namespace Game.Blocks;

public class IronOreChunkBlock() : ChunkBlock(
    Matrix.CreateRotationX(0f) * Matrix.CreateRotationZ(2f),
    Matrix.CreateTranslation(0.9375f, 0.1875f, 0f),
    new Color(136, 74, 36),
    false
)
{
    public const int Index = 249;
}
