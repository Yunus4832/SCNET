namespace Game.Modding.Blocks;

/// <summary>Materialized rule and asset metadata text; contains no Host reader callback.</summary>
public sealed record BlockDataEntry(ResourceId Id, string Data);
