using MessagePack;

namespace Game.Commands;

[MessagePackObject]
public sealed record MarkPrivatePositionCommand([property: Key(0)] string Name) : IGameCommand;
