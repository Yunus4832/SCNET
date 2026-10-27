using MessagePack;

namespace Game.Commands;

[MessagePackObject]
public sealed record TeleportMarkCommand([property: Key(0)] string Name, [property: Key(1)] bool Public) : IGameCommand;
