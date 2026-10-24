using Game.Protocol.Connection;

namespace Game.Protocol.Characters;

public sealed record CharacterSnapshot(byte[] Data, ClientIdentity? Client = null);
