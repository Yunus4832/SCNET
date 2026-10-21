namespace Game.Content;

public sealed class ContentRendezvousCatalog(ContentServerClientPool pool)
{
    public async Task<RendezvousCatalogResult> QueryAsync(
        IEnumerable<ContentRepository> repositories,
        CancellationToken cancellationToken
    )
    {
        var configured = repositories.ToArray();
        pool.Update(Guid.Empty, configured);
        var services = new List<RegisteredRendezvousService>();
        var failures = new HashSet<Guid>();
        foreach (var repository in configured.Where(item => item.IsEnabled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var lease = pool.Acquire(Guid.Empty, repository.Id);
                var records = await lease.Client.ListRendezvousServicesAsync(cancellationToken).ConfigureAwait(false);
                services.AddRange(
                    records.Select(record => new RegisteredRendezvousService(
                            repository.Id,
                            repository.Name,
                            record.Id,
                            record.Name,
                            record.ControlUrl,
                            record.Description
                        )
                    )
                );
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures.Add(repository.Id);
                Log.Warning($"Could not load signaling services from '{repository.Name}': {exception.Message}");
            }
        }

        return new RendezvousCatalogResult(services, failures);
    }
}

public sealed record RegisteredRendezvousService(
    Guid RepositoryId,
    string RepositoryName,
    string RegistrationId,
    string Name,
    string ControlUrl,
    string? Description
);

public sealed record RendezvousCatalogResult(
    IReadOnlyList<RegisteredRendezvousService> Services,
    IReadOnlySet<Guid> UnavailableRepositories
);
