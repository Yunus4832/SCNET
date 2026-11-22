using Game.Protocol.Identity;

namespace Game.Players;

/// <summary>持久化的世界玩家主体，角色资源和运行时连接由其他对象管理。</summary>
public sealed class WorldPlayer
{
    internal WorldPlayer(PlayerAddress address)
    {
        _ = address.Value;
        Address = address;
    }

    public PlayerAddress Address { get; }

    public string DisplayName { get; set; } = string.Empty;

    public Guid CharacterId { get; internal set; }

    public CommandPermissionSet Permissions { get; } = new();

    public bool IsMuted { get; set; }

    public PlayerBan? Ban { get; internal set; }
}
