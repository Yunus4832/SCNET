using MessagePack;

namespace Game.Commands.BuiltIn;

[MessagePackObject]
public sealed record MarkPrivatePositionCommand([property: Key(0)] string Name) : IGameCommand;
