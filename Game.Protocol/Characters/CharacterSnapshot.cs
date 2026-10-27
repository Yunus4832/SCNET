using Game.Protocol.Connection;

using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterSnapshot(
    [property: Key(0)] byte[] Data,
    [property: Key(1)] ClientIdentity? Client = null
);
