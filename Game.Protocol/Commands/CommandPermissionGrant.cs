using MessagePack;

namespace Game.Protocol.Commands;

[MessagePackObject]
public sealed record CommandPermissionGrant(
    [property: Key(0)] ResourceId Permission,
    [property: Key(1)] bool CanDelegate
);
