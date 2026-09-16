namespace ServerSource.Protocol;

public sealed record ServerSourceEntry(
    string Id,
    string Name,
    string Address,
    string? Description,
    IReadOnlyList<string> Tags);
