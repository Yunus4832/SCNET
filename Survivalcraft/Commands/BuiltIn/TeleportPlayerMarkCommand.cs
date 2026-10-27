using MessagePack;

namespace Game.Commands;

[MessagePackObject]
public sealed record TeleportPlayerMarkCommand(
    [property: Key(0)] string Player,
    [property: Key(1)] string Name,
    [property: Key(2)] bool Public
) : IGameCommand;
