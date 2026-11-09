namespace Game.Terrains.Distribution;

/// <summary>Authority-only progress of acquiring chunk contents, independent of derived data.</summary>
public enum TerrainGenerationStage
{
    Restore = 0,

    FirstPass = 1,

    SecondPass = 2,

    ThirdPass = 3,

    FourthPass = 4,

    Publication = 5,

    Complete = 6,

    Rejected = 7,

    Publishing = 8
}
