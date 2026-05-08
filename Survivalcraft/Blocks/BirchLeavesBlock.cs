namespace Game.Blocks;

public class BirchLeavesBlock() : DeciduousLeavesBlock(
    0.95f,
    0.1f,
    0.5f,
    0.83f,
    BlockColorsMap.BirchLeavesColorsMap,
    new Color(220, 170, 30),
    new Color(255, 230, 70),
    1.25f
)
{
    public const int Index = 13;

    public override int GetFaceTextureSlot(int face, int value)
    {
        return GetSeason(Terrain.ExtractData(value)) == Season.Winter ? 106 : base.GetFaceTextureSlot(face, value);
    }
}
