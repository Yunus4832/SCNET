using Game.Protocol.Identity;

namespace Game.Players;

/// <summary>仅在本世界生效的准入限制，由世界保存，不与外部认证机构同步。</summary>
public sealed record PlayerBan(string Reason, PlayerAddress? Actor, DateTimeOffset CreatedAt);
