using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public readonly record struct CharacterEntitySnapshot(
    [property: Key(0)] Guid CharacterId,
    [property: Key(1)] int EntityId,
    [property: Key(2)] Vector3 Position,
    [property: Key(3)] int ParentBodyId
);
