using Engine.Core.Mathematics;

namespace Game.Protocol.Players;

public readonly record struct OnlinePlayerState(
    Guid PlayerGuid,
    Vector3 Position,
    float Health,
    bool IsSleeping
);
