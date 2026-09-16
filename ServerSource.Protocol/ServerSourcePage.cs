namespace ServerSource.Protocol;

public sealed record ServerSourcePage(
    int ProtocolVersion,
    ServerSourceDescriptor Source,
    IReadOnlyList<ServerSourceEntry> Servers,
    string? NextCursor);
