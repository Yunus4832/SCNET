using MessagePack;

namespace Game.Commands.BuiltIn;

[MessagePackObject]
public sealed record MarkPublicPositionCommand([property: Key(0)] string Name) : IGameCommand;
