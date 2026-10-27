using MessagePack;

namespace Game.Protocol.World;

[MessagePackObject]
public sealed record WorldMetadata(
    [property: Key(0)] string Name,
    [property: Key(1)] int Seed,
    [property: Key(2)] byte Mode,
    [property: Key(3)] ushort MaximumPlayers
);
