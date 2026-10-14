namespace Game.Commands;

public sealed record StartP2PCommand : IGameCommand;

public sealed record StopP2PCommand : IGameCommand;

public sealed record GetP2PStatusCommand : IGameCommand;
