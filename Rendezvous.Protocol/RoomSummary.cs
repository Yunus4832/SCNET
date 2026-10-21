namespace Rendezvous.Protocol;

public sealed record RoomSummary(
    Guid Id,
    string Name,
    string Description,
    string JoinCode,
    IReadOnlyList<string> ConnectionMethods
);
