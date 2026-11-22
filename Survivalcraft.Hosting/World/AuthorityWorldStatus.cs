namespace Game.World;

/// <summary>不可变的宿主观测结果，不包含活动项目、实体或连接集合。</summary>
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
