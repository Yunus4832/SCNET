namespace Game.Terrains.Distribution;

/// <summary>
///     Describes whether a terrain instance produces authoritative contents or derives client data
///     from installed snapshots. This is intentionally independent from process/network mode.
/// </summary>
public enum TerrainContentRole
{
    Authority,
    Replica
}
