namespace Game.World;

/// <summary>Immutable host observation; contains no live project, entity or connection collection.</summary>
public sealed record AuthorityWorldStatus(
    Guid WorldId,
    AuthorityExecutionState State,
    bool Paused,
    long TickIndex,
    long ServiceCycles,
    int PendingRequests,
    bool Listening,
    int PlayerCount,
    double MaximumTickMilliseconds,
    int? LocalPort,
    IReadOnlyList<AuthorityCharacterStatus> Characters
);
