namespace Game.Network;

/// <summary>Immutable effective interest settings for one world lifetime; range clamping belongs to NetworkTerrainPolicy.</summary>
public sealed record NetworkInterestPolicy(int MaxClientVisibilityRange);
