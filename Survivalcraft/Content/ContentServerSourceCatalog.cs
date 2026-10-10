namespace Game.Content;

public sealed class ContentServerSourceCatalog(ContentServerClientPool pool)
{
    public async Task<IReadOnlyList<RegisteredServerSource>> QueryAsync(
        IEnumerable<ContentRepository> repositories,
        CancellationToken cancellationToken = default)
    {
        var configuredRepositories = repositories.ToArray();
        pool.Update(Guid.Empty, configuredRepositories);
        var results = new List<RegisteredServerSource>();
        foreach (var repository in configuredRepositories.Where(repository => repository.IsEnabled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var lease = pool.Acquire(Guid.Empty, repository.Id);
                var sources = await lease.Client.ListServerSourcesAsync(cancellationToken).ConfigureAwait(false);
                results.AddRange(sources.Select(source => new RegisteredServerSource(repository.Id, repository.Name,
                    source.Id, source.Name, source.ApiUrl, source.Description)));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Log.Warning($"Could not load server sources from content repository '{repository.Name}': " +
                            exception.Message);
            }
        }

        return results;
    }
}

public sealed record RegisteredServerSource(
    Guid RepositoryId,
    string RepositoryName,
    string RegistrationId,
    string Name,
    string ApiUrl,
    string? Description);
