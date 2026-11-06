namespace Game.Content;

public sealed class ContentAuthenticationCatalog(ContentServerClientPool pool)
{
    public async Task<AuthenticationCatalogResult> QueryAsync(
        IEnumerable<ContentRepository> repositories,
        CancellationToken cancellationToken
    )
    {
        var configured = ContentRepository.NormalizeAll(repositories).ToArray();
        pool.Update(Guid.Empty, configured);
        using var concurrency = new SemaphoreSlim(4);
        var results = await Task.WhenAll(
            configured.Where(repository => repository.IsEnabled).Select(async repository =>
                {
                    await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        using var lease = pool.Acquire(Guid.Empty, repository.Id);
                        var records = await lease.Client.ListAuthenticationServicesAsync(cancellationToken)
                            .ConfigureAwait(false);
                        return (Repository: repository.Id,
                            Records: records.Select(record => new RegisteredAuthenticationService(
                                    repository.Id,
                                    repository.Name,
                                    repository.BaseUrl,
                                    record.Id,
                                    record.Name,
                                    record.ServiceUrl,
                                    record.Description,
                                    record.ReviewedAt!.Value
                                )
                            ).ToArray(), Failed: false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception)
                    {
                        Log.Warning($"Could not load authentication services from '{repository.Name}'.");
                        return (Repository: repository.Id, Records: Array.Empty<RegisteredAuthenticationService>(),
                            Failed: true);
                    }
                    finally { concurrency.Release(); }
                }
            )
        ).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return new AuthenticationCatalogResult(
            results.SelectMany(result => result.Records).ToArray(),
            results.Where(result => result.Failed).Select(result => result.Repository).ToHashSet()
        );
    }
}
