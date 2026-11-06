namespace Game.Terrains.Rendering;

/// <summary>Client mesh generation phases; CPU terrain readiness is independent.</summary>
public enum TerrainMeshState
{
    NeedsFirstPass = 0,

    NeedsSecondPass = 1,

    Ready = 2
}
