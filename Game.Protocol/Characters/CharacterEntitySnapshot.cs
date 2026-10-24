using Engine.Core.Mathematics;

namespace Game.Protocol.Characters;

public readonly record struct CharacterEntitySnapshot(
    Guid CharacterId,
    int EntityId,
    Vector3 Position,
    int ParentBodyId
);
