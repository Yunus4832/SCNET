namespace Engine.Network;

/// <summary>当前活动资源数的近似值；宿主停止后逐步归零。</summary>
public readonly record struct NetworkHostResources(
    int Connections,
    int PendingConnections,
    int PendingSignals,
    int CandidatePaths,
    int Registrations,
    int RoomRegistrations,
    int RelayPreparations,
    int RelayRoutes,
    int Notifications,
    int SecureSessions,
    int PendingSecureHandshakes
);
