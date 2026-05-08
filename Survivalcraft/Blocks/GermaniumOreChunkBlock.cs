namespace Game.Blocks;

public class GermaniumOreChunkBlock() : ChunkBlock(
    Matrix.CreateRotationX(-1f) * Matrix.CreateRotationZ(1f),
    Matrix.CreateTranslation(0.0625f, 0.4375f, 0f),
    new Color(204, 181, 162),
    false
)
{
    public const int Index = 250;
}
