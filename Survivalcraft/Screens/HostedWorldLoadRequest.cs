namespace Game.Screens;

/// <summary>Requests loading a hosted world, with optional session overrides; this navigation request is never persisted.</summary>
public sealed record HostedWorldLoadRequest(SessionInfo? Session = null);
