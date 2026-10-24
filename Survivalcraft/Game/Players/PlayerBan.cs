using Game.Protocol.Identity;

namespace Game.Players;

/// <summary>A world-local admission restriction; external authorities do not own or synchronize this record.</summary>
public sealed record PlayerBan(string Reason, PlayerAddress? Actor, DateTimeOffset CreatedAt);
