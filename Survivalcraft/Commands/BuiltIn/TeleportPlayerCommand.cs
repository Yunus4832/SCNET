namespace Game.Commands;

public sealed record TeleportPlayerCommand(string Player, Vector3? Position = null, string? DestinationPlayer = null) : IGameCommand;
