namespace Game.Blocks;

public class PoplarLeavesBlock() : DeciduousLeavesBlock(
    0.97f, 0.17f, 0.52f, 0.84f,
    BlockColorsMap.PoplarLeavesColorsMap,
    new Color(220, 130, 20),
    new Color(255, 190, 60),
    1.5f
)
{
    public const int Index = 263;

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
