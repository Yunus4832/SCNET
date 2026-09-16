namespace ServerSource.Protocol;

public sealed record ServerSourceSnapshot(
    ServerSourceDescriptor Source,
    IReadOnlyList<ServerSourceEntry> Servers);
