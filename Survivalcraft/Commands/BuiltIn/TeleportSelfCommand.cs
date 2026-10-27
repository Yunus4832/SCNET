using MessagePack;

namespace Game.Commands;

[MessagePackObject]
public sealed record TeleportSelfCommand(
    [property: Key(0)] Vector3? Position = null,
    [property: Key(1)] string? DestinationPlayer = null
) : IGameCommand;
