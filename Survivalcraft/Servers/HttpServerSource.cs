using ServerSource.Protocol;

namespace Game.Servers;

public sealed class HttpServerSource(
    ServerSourceSubscription subscription,
    ServerSourceProtocolClient client
) : IServerSource
{
    public string Id => $"http.{subscription.Id:N}";

    public string Name => subscription.Name;

    public ServerSourceKind Kind => ServerSourceKind.Http;

    public async Task<IReadOnlyList<ServerItem>> LoadAsync(CancellationToken cancellationToken)
    {
        var snapshot = await client.GetAllAsync(new Uri(subscription.ApiUrl), cancellationToken)
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
