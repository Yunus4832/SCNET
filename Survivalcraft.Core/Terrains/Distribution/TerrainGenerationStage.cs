namespace Game.Terrains.Distribution;

/// <summary>权威端获取区块内容的进度，独立于派生数据的准备过程。</summary>
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
