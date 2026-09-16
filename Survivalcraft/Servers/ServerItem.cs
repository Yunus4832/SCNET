namespace Game.Servers;

public sealed record ServerItem
{
    public required string SourceId { get; init; }

    public required string EntryId { get; init; }

    public required ServerSourceKind SourceKind { get; init; }

    public required string SourceName { get; init; }

    public required string Address { get; init; }

    public required string DisplayName { get; init; }

    public string Description { get; init; } = string.Empty;

    public IReadOnlyList<string> Tags { get; init; } = [];

    public int Order { get; init; }

    public ServerRuntimeStatus RuntimeStatus { get; set; } = new();

    public string Identity => $"{SourceId}\n{EntryId}";
}
