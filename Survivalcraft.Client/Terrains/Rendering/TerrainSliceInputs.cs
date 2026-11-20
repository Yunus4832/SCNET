namespace Game.Terrains.Rendering;

/// <summary>Fingerprints the geometry and sampled illumination of a terrain slice independently.</summary>
internal readonly record struct TerrainSliceInputs(int Geometry, ulong Lighting);
