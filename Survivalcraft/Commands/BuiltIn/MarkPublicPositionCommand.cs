using MessagePack;

namespace Game.Commands;

[MessagePackObject]
public sealed record MarkPublicPositionCommand([property: Key(0)] string Name) : IGameCommand;
