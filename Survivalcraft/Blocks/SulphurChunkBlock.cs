namespace Game.Blocks;

public class SulphurChunkBlock() : ChunkBlock(
    Matrix.CreateRotationX(2f) * Matrix.CreateRotationZ(1f),
    Matrix.CreateTranslation(0.0625f, 0.4375f, 0f),
    new Color(255, 255, 140),
    true
)
{
    public const int Index = 103;
}
