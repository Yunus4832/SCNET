namespace Game.Blocks;

public class CoalChunkBlock() : ChunkBlock(
    Matrix.CreateRotationX(1f) * Matrix.CreateRotationZ(2f),
    Matrix.CreateTranslation(0.875f, 0.1875f, 0f),
    new Color(255, 255, 255),
    false
)
{
    public const int Index = 22;
}
