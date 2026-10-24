using Game.Protocol.Identity;

namespace Game.Protocol.Connection;

public sealed record ClientIdentity(
    byte ID,
    Guid TokenId,
    Guid CharacterId,
    PlayerAddress? PlayerAddress,
    ClientState State = ClientState.NotConnected
);
