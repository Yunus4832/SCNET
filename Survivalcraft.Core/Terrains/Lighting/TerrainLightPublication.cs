namespace Game.Terrains.Lighting;

/// <summary>Publication outcome consumed by the owning world's dependency scheduler.</summary>
public readonly record struct TerrainLightPublication(bool Published, bool ValuesChanged, int ChangedBoundaryFaces);
