namespace Game.Admission;

/// <summary>Resolved GUI host session overrides carried to the loading workflow; never persisted separately.</summary>
public sealed record HostedWorldStartup(SessionInfo? Session = null);
