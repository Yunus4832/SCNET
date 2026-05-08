namespace Game.Blocks;

public class OakLeavesBlock() : DeciduousLeavesBlock(
    0f, 0.25f, 0.54f, 0.85f,
    BlockColorsMap.OakLeavesColorsMap,
    new Color(230, 80, 0),
    new Color(255, 130, 20),
    2f
)
{
    public const int Index = 12;

    public override int GetFaceTextureSlot(int face, int value)
    {
        var season = GetSeason(Terrain.ExtractData(value));
        return season switch
        {
            Season.Winter => 106,
            Season.Spring => 107,
            _ => base.GetFaceTextureSlot(face, value)
        };
    }
}
