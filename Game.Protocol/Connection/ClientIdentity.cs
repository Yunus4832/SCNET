using Game.Protocol.Identity;

using MessagePack;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record ClientIdentity(
    [property: Key(0)] byte ID,
    [property: Key(1)] Guid TokenId,
    [property: Key(2)] Guid CharacterId,
    [property: Key(3)] PlayerAddress? PlayerAddress,
    [property: Key(4)] ClientState State = ClientState.NotConnected
);
