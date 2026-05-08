namespace Game.Blocks;

public class GermaniumChunkBlock() : ChunkBlock(
    Matrix.CreateRotationX(3f) * Matrix.CreateRotationZ(2f),
    Matrix.CreateTranslation(0.875f, 0.25f, 0f),
    new Color(255, 255, 255),
    false
)
{
    public const int Index = 149;
}
