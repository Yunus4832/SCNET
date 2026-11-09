namespace Game.Terrains;

/// <summary>Content acquisition, separate from whether its arrays are currently available.</summary>
public enum TerrainContentOrigin
{
    Unspecified = 0,

    Storage = 1,

    Generated = 2,

    Replica = 3
}
