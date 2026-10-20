using Game.Protocol.Connection;

namespace Game.Protocol.Players;

public sealed record PlayerSnapshot(byte[] Data, ClientIdentity? Client = null);
