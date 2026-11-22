namespace Game.Terrains;

/// <summary>记录内容的获取方式，数组当前是否可用另行判断。</summary>
public enum TerrainContentOrigin
{
    Unspecified = 0,

    Storage = 1,

    Generated = 2,

    Replica = 3
}
