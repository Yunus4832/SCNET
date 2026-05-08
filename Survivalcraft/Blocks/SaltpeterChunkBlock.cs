namespace Game.Blocks;

public class SaltpeterChunkBlock() : ChunkBlock(
    Matrix.CreateRotationX(1f) * Matrix.CreateRotationZ(0f),
    Matrix.CreateTranslation(0.0625f, 0.4375f, 0f),
    new Color(255, 255, 255),
    false
)
{
    public const int Index = 102;
}
