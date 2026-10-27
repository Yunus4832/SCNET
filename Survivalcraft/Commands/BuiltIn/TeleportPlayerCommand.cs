using MessagePack;

namespace Game.Commands;

[MessagePackObject]
public sealed record TeleportPlayerCommand(
    [property: Key(0)] string Player,
    [property: Key(1)] Vector3? Position = null,
    [property: Key(2)] string? DestinationPlayer = null
) : IGameCommand;
