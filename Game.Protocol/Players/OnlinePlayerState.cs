using Engine.Core;

namespace Game.Protocol.Players;

public readonly record struct OnlinePlayerState(
    Guid PlayerGuid,
    Vector3 Position,
    float Health,
    bool IsSleeping
);
