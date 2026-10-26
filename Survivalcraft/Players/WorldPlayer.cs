using Game.Commands;
using Game.Protocol.Identity;

namespace Game.Players;

/// <summary>Persistent world subject. Character assets and runtime connections belong to other objects.</summary>
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
