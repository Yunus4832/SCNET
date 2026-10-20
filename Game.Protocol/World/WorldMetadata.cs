namespace Game.Protocol.World;

public sealed record WorldMetadata(string Name, int Seed, byte Mode, ushort MaximumPlayers);
