using Game.Protocol.Identity;

namespace Game.Protocol.Characters;

public sealed record CharacterListEntry(
    Guid CharacterId,
    PlayerAddress OwnerAddress,
    string Name,
    bool IsOnline
);
