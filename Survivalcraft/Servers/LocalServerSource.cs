namespace Game.Servers;

public sealed class LocalServerSource(ServerDirectoryManager service) : IServerSource
{
    public string Id => ServerSourceIds.Local;

    public string Name => "Local";

    public ServerSourceKind Kind => ServerSourceKind.Local;

    public Task<IReadOnlyList<ServerItem>> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ServerItem> items = service.Snapshot().LocalServers.Select(entry => new ServerItem
        {
            SourceId = Id,
            EntryId = entry.Id.ToString("N"),
            SourceKind = Kind,
            SourceName = Name,
            Address = entry.Address,
            DisplayName = entry.Name,
            LocalTags = entry.Tags,
            UpdatedAt = entry.UpdatedAt,
            Order = entry.Order
        }).ToArray();
        return Task.FromResult(items);
    }
}
