namespace Game.Modding.Blocks;

/// <summary>Host resource registration; materialized before Core catalog initialization.</summary>
public sealed record BlockDataRegistration(Func<string> Read);
