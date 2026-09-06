using System.Net;
using System.Text;

using Game.Content;

namespace Survivalcraft.Test.Modding;

public sealed class ContentRepositoryServiceTest
{
    [Fact]
    public async Task ConnectionTestUsesConfiguredClientAndValidatesHealthResponse()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"success\":true,\"message\":\"\",\"code\":200,\"data\":{\"name\":\"ContentServer\",\"version\":\"v1\"}}",
                Encoding.UTF8, "application/json")
        });
        using var pool = new ContentServerClientPool(new StubFactory(handler));
        var repository = new ContentRepository { Name = "A", BaseUrl = "https://a.example" };
        var service = new ContentRepositoryService([repository], pool, _ => { });

        var health = await service.TestConnectionAsync(repository.Id);

        Assert.Equal(new ContentServerHealth("ContentServer", "v1"), health);
        Assert.Equal("https://a.example/api/v1/health", handler.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task ConnectionTestRejectsInvalidHealthResponse()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":true,\"data\":{}}", Encoding.UTF8, "application/json")
        });
        using var pool = new ContentServerClientPool(new StubFactory(handler));
        var repository = new ContentRepository { Name = "A", BaseUrl = "https://a.example" };
        var service = new ContentRepositoryService([repository], pool, _ => { });

        await Assert.ThrowsAsync<InvalidDataException>(() => service.TestConnectionAsync(repository.Id));
    }

    [Fact]
    public async Task ConnectionTestRejectsDisabledRepositoryWithoutNetworkAccess()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("unexpected request"));
        using var pool = new ContentServerClientPool(new StubFactory(handler));
        var repository = new ContentRepository
        {
            Name = "A",
            BaseUrl = "https://a.example",
            IsEnabled = false
        };
        var service = new ContentRepositoryService([repository], pool, _ => { });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TestConnectionAsync(repository.Id));
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public void SetOrderPersistsContiguousPriorities()
    {
        using var pool = new ContentServerClientPool(new ContentServerClientFactory());
        IReadOnlyList<ContentRepository> saved = [];
        var first = new ContentRepository { Name = "A", BaseUrl = "https://a.example", Priority = 10 };
        var second = new ContentRepository { Name = "B", BaseUrl = "https://b.example", Priority = 20 };
        var service = new ContentRepositoryService([first, second], pool, items => saved = items);

        service.SetOrder([second.Id, first.Id]);

        Assert.Equal([second.Id, first.Id], service.Snapshot().Select(item => item.Id));
        Assert.Equal([0, 1], saved.Select(item => item.Priority));
        Assert.Throws<ArgumentException>(() => service.SetOrder([first.Id, first.Id]));
    }

    [Fact]
    public void EditsPersistAndRefreshPoolWithStableIdentity()
    {
        using var pool = new ContentServerClientPool(new ContentServerClientFactory());
        IReadOnlyList<ContentRepository> saved = [];
        var service = new ContentRepositoryService([], pool, items => saved = items);
        var repository = new ContentRepository { Name = "A", BaseUrl = "https://a.example" };
        service.Add(repository);
        using var original = pool.Acquire(Guid.Empty, repository.Id);
        service.Edit(repository with { Name = "B", BaseUrl = "https://b.example", Priority = 2 });
        Assert.Equal(repository.Id, Assert.Single(saved).Id);
        Assert.Equal("B", Assert.Single(service.Snapshot()).Name);
        using var replacement = pool.Acquire(Guid.Empty, repository.Id);
        Assert.NotSame(original.Client, replacement.Client);
        service.Delete(repository.Id);
        Assert.Empty(saved);
        Assert.Throws<KeyNotFoundException>(() => pool.Acquire(Guid.Empty, repository.Id));
    }

    [Fact]
    public void FailedPersistenceLeavesConfigurationAndClientsUnchanged()
    {
        using var pool = new ContentServerClientPool(new ContentServerClientFactory());
        var repository = new ContentRepository { Name = "A", BaseUrl = "https://a.example" };
        var service = new ContentRepositoryService([repository], pool, _ => throw new IOException("disk failure"));
        using var original = pool.Acquire(Guid.Empty, repository.Id);
        Assert.Throws<IOException>(() => service.Delete(repository.Id));
        Assert.Equal(repository, Assert.Single(service.Snapshot()));
        using var afterFailure = pool.Acquire(Guid.Empty, repository.Id);
        Assert.Same(original.Client, afterFailure.Client);
    }

    private sealed class StubFactory(HttpMessageHandler handler) : ContentServerClientFactory
    {
        public override ContentServerClient Create(ContentRepository repository)
        {
            return new ContentServerClient(repository.BaseUrl, new HttpClient(handler), true);
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(handler(request));
        }
    }
}
