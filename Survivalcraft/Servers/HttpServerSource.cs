using ServerSource.Protocol;

namespace Game.Servers;

public sealed class HttpServerSource(
    InstalledServerSource installedSource,
    ServerSourceProtocolClient client
) : IServerSource
{
    public string Id => $"http.{installedSource.Id:N}";

    public string Name => installedSource.Name;

    public ServerSourceKind Kind => ServerSourceKind.Http;

    public async Task<IReadOnlyList<ServerItem>> LoadAsync(CancellationToken cancellationToken)
    {
        var snapshot = await client.GetAllAsync(new Uri(installedSource.ApiUrl), cancellationToken)
            .ConfigureAwait(false);
        return snapshot.Servers.Select((entry, order) => new ServerItem
        {
            SourceId = Id,
            EntryId = entry.Id,
            SourceKind = Kind,
            SourceName = snapshot.Source.Name,
            Address = entry.Address,
            DisplayName = entry.Name,
            Description = entry.Description ?? string.Empty,
            Tags = entry.Tags,
            Order = order
        }).ToArray();
    }
}
