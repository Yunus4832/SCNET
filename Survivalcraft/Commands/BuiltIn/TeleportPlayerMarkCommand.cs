namespace Game.Commands;

public sealed record TeleportPlayerMarkCommand(string Player, string Name, bool Public) : IGameCommand;
