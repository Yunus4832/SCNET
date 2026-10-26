namespace Game.Servers;

public sealed record P2PRoomStatus(
    P2PRoomState State,
    bool DesiredEnabled,
    bool ListenerReady,
    bool WorldReady,
    string ServiceName,
    string ControlUrl,
    string Name,
    string Description,
    bool IsPublic,
    int Capacity,
    Guid? SessionId,
    string? JoinCode,
    string LastError,
    DateTimeOffset? NextRetryAt
);
