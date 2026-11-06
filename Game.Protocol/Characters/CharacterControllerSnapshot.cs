using Game.Protocol.Connection;
using Game.Protocol.Identity;

using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterControllerSnapshot(
    [property: Key(0)] byte ConnectionId,
    [property: Key(1)] Guid CharacterId,
    [property: Key(2)] PlayerAddress? PlayerAddress,
    [property: Key(3)] ClientState State,
    [property: Key(4)] Guid ConnectionEpoch,
    [property: Key(5)] long ConnectionGeneration
);
