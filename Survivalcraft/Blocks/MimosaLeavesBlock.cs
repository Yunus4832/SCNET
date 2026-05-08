namespace Game.Blocks;

public class MimosaLeavesBlock() : DeciduousLeavesBlock(
    0f, 0.25f, 0.54f, 0.85f,
    BlockColorsMap.MimosaLeavesColorsMap,
    new Color(192, 100, 0),
    new Color(192, 150, 0),
    1.25f
)
{
    public const int Index = 256;

    public override int GetFaceTextureSlot(int face, int value)
    {
        return GetSeason(Terrain.ExtractData(value)) == Season.Winter ? 106 : base.GetFaceTextureSlot(face, value);
    }
}
