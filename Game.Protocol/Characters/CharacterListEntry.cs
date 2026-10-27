using Game.Protocol.Identity;

using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterListEntry(
    [property: Key(0)] Guid CharacterId,
    [property: Key(1)] PlayerAddress OwnerAddress,
    [property: Key(2)] string Name,
    [property: Key(3)] bool IsOnline
);
