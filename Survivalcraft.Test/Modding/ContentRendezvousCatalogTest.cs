using System.Net;
using System.Net.Http.Json;

using Game.Content;

namespace Survivalcraft.Test.Modding;

public sealed class ContentRendezvousCatalogTest
{
    [Fact]
    public async Task PreservesSuccessfulRepositoryResultsAlongsideExplicitFailures()
    {
        var good = new ContentRepository { Name = "Good", BaseUrl = "https://good.example/" };
        var failed = new ContentRepository { Name = "Failed", BaseUrl = "https://failed.example/" };
        var disabled = new ContentRepository { Name = "Disabled", BaseUrl = "https://disabled.example/", IsEnabled = false };
        var factory = new CatalogFactory();
        using var pool = new ContentServerClientPool(factory);
        var result = await new ContentRendezvousCatalog(pool).QueryAsync([good, failed, disabled], CancellationToken.None);
        var service = Assert.Single(result.Services);
        Assert.Equal(good.Id, service.RepositoryId);
        Assert.Equal("Good", service.RepositoryName);
        Assert.Equal("registered-service", service.RegistrationId);
        Assert.Equal(failed.Id, Assert.Single(result.UnavailableRepositories));
        Assert.Equal(2, factory.Created.Count);
        Assert.DoesNotContain(disabled.Id, factory.Created);
    }

    [Fact]
    public async Task CallerCancellationIsNotReportedAsAnUnavailableRepository()
    {
        var factory = new CatalogFactory();
        using var pool = new ContentServerClientPool(factory);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ContentRendezvousCatalog(pool)
            .QueryAsync([new ContentRepository { Name = "Good", BaseUrl = "https://good.example/" }], cancellation.Token));
        Assert.Empty(factory.Created);
    }

    private sealed class CatalogFactory : ContentServerClientFactory
    {
        public List<Guid> Created { get; } = [];

        public override ContentServerClient Create(ContentRepository repository)
        {
            Created.Add(repository.Id);
            return new ContentServerClient(repository.BaseUrl, new HttpClient(new CatalogHandler()), true);
        }
    }

    private sealed class CatalogHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host == "failed.example")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    success = true,
                    data = new
                    {
                        items = new[] { new ContentServerRendezvousService("registered-service", "Signal", "https://signal.example/", null) },
                        total = 1,
                        pageIndex = 1,
                        pageSize = 100
                    }
                })
            });
        }
    }
}
