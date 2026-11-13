namespace Game.Terrains.Rendering;

/// <summary>Client mesh generation phases; CPU terrain readiness is independent.</summary>
public enum TerrainMeshState
{
    Building = 0,

    Ready = 2
}
