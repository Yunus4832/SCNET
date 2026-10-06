namespace Game.Commands;

public sealed record TeleportMarkCommand(string Name, bool Public) : IGameCommand;
