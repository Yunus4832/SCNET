namespace Game.Commands;

public sealed record TeleportSpawnCommand(bool World) : IGameCommand;
