namespace Game.Commands;

public sealed record TeleportSelfCommand(Vector3? Position = null, string? DestinationPlayer = null) : IGameCommand;
