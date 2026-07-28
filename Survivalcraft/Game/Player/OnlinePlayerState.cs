namespace Game;

public readonly record struct OnlinePlayerState(
    Guid PlayerGuid,
    Vector3 Position,
    float Health,
    bool IsSleeping
);
