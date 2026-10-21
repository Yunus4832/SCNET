namespace Game.Protocol.Commands;

public sealed record CommandPermissionGrant(
    ResourceId Permission,
    bool CanDelegate
);
