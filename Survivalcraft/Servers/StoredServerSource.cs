namespace Game.Servers;

public sealed class StoredServerSource : IServerSource
{
    private readonly Func<ServerDirectoryState, IReadOnlyList<StoredServerEntry>> _selectEntries;
    private readonly ServerDirectoryService _service;

    public StoredServerSource(string id, string name, ServerSourceKind kind, ServerDirectoryService service,
        Func<ServerDirectoryState, IReadOnlyList<StoredServerEntry>> selectEntries)
    {
        Id = id;
        Name = name;
        Kind = kind;
        _service = service;
        _selectEntries = selectEntries;
    }

    public string Id { get; }

    public string Name { get; }

    public ServerSourceKind Kind { get; }

    public Task<IReadOnlyList<ServerItem>> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = _selectEntries(_service.Snapshot());
        IReadOnlyList<ServerItem> items = entries.Select(entry => new ServerItem
        {
            SourceId = Id,
            EntryId = entry.Id.ToString("N"),
            SourceKind = Kind,
            SourceName = Name,
            Address = entry.Address,
            DisplayName = entry.Name,
            Order = entry.Order
        }).ToArray();
        return Task.FromResult(items);
    }
}
