using MessagePack;

namespace Game.Commands;

[MessagePackObject]
public sealed record StartP2PCommand : IGameCommand;

[MessagePackObject]
public sealed record StopP2PCommand : IGameCommand;

[MessagePackObject]
public sealed record GetP2PStatusCommand : IGameCommand;
