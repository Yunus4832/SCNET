using Engine.Core.Mathematics;

namespace Game.Protocol.Characters;

public readonly record struct OnlineCharacterState(
    Guid CharacterId,
    Vector3 Position,
    float Health,
    bool IsSleeping
);
