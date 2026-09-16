namespace Game.Servers;

public interface IServerSource
{
    string Id { get; }

    string Name { get; }

    ServerSourceKind Kind { get; }

    Task<IReadOnlyList<ServerItem>> LoadAsync(CancellationToken cancellationToken);
}
