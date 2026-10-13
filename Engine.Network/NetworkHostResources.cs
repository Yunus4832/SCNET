namespace Engine.Network;

/// <summary>Approximate live resource counts; stopped hosts converge to zero.</summary>
public readonly record struct NetworkHostResources(
    int Connections,
    int PendingConnections,
    int PendingSignals,
    int CandidatePaths,
    int Registrations,
    int Invitations,
    int RelayPreparations,
    int RelayRoutes,
    int Notifications);
