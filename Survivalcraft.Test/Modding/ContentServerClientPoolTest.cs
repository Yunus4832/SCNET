using System.Net;

using Game.Content;

namespace Survivalcraft.Test.Modding;

public sealed class ContentServerClientPoolTest
{
    [Fact]
    public void LazyCreationUsesLatestRepositoryMetadata()
    {
        var factory = new RecordingFactory();
        using var pool = new ContentServerClientPool(factory);
        var repository = new ContentRepository { Name = "A", BaseUrl = "https://a.example" };
        pool.Update(Guid.Empty, [repository]);
        var edited = repository with { Name = "Renamed", Priority = 3 };
        pool.Update(Guid.Empty, [edited]);
        using var lease = pool.Acquire(Guid.Empty, repository.Id);
        Assert.Equal(edited, factory.CreatedFrom);
    }

    private sealed class RecordingFactory : ContentServerClientFactory
    {
        public ContentRepository? CreatedFrom { get; private set; }

        public override ContentServerClient Create(ContentRepository repository)
        {
            CreatedFrom = repository;
            return base.Create(repository);
        }
    }

    [Fact]
    public void ReusesClientsAndRetiresOldAddressWithoutInvalidatingLeases()
    {
        using var pool = new ContentServerClientPool(new ContentServerClientFactory());
        var repository = new ContentRepository { Name = "A", BaseUrl = "https://a.example" };
        var scope = Guid.NewGuid();
        pool.Update(scope, [repository]);
        using var first = pool.Acquire(scope, repository.Id);
        using var second = pool.Acquire(scope, repository.Id);
        Assert.Same(first.Client, second.Client);
        pool.Update(scope, [repository with { BaseUrl = "https://b.example" }]);
        using var replacement = pool.Acquire(scope, repository.Id);
        Assert.NotSame(first.Client, replacement.Client);
        pool.RemoveScope(scope);
        Assert.Throws<KeyNotFoundException>(() => pool.Acquire(scope, repository.Id));
        Assert.Throws<InvalidOperationException>(() => pool.Update(scope, [repository]));
        first.Dispose();
        first.Dispose();
    }

    [Fact]
    public void SeparatesScopesAndRejectsDisabledRepositories()
    {
        using var pool = new ContentServerClientPool(new ContentServerClientFactory());
        var repository = new ContentRepository { Name = "A", BaseUrl = "https://a.example" };
        var session = Guid.NewGuid();
        pool.Update(Guid.Empty, [repository]);
        pool.Update(session, [repository]);
        using var persistent = pool.Acquire(Guid.Empty, repository.Id);
        using var temporary = pool.Acquire(session, repository.Id);
        Assert.NotSame(persistent.Client, temporary.Client);
        pool.Update(session, [repository with { IsEnabled = false }]);
        Assert.Throws<KeyNotFoundException>(() => pool.Acquire(session, repository.Id));
        using var stillEnabled = pool.Acquire(Guid.Empty, repository.Id);
        Assert.Same(persistent.Client, stillEnabled.Client);
    }

    [Fact]
    public async Task RetiredClientRemainsAliveUntilRequestLeaseIsReleased()
    {
        var handler = new PendingHandler();
        using var pool = new ContentServerClientPool(new OwnedClientFactory(handler));
        var repository = new ContentRepository { Name = "A", BaseUrl = "https://a.example" };
        pool.Update(Guid.Empty, [repository]);
        var lease = pool.Acquire(Guid.Empty, repository.Id);
        var request = lease.Client.ListAsync();
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        pool.Update(Guid.Empty, [repository with { IsEnabled = false }]);
        Assert.False(handler.IsDisposed);
        handler.Complete();
        await request;
        Assert.False(handler.IsDisposed);

        lease.Dispose();
        Assert.True(handler.IsDisposed);
    }

    private sealed class OwnedClientFactory(PendingHandler handler) : ContentServerClientFactory
    {
        public override ContentServerClient Create(ContentRepository repository)
        {
            return new ContentServerClient(repository.BaseUrl, new HttpClient(handler), true);
        }
    }

    private sealed class PendingHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<HttpResponseMessage> _response =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsDisposed { get; private set; }

        public void Complete()
        {
            _response.SetResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"success\":true,\"data\":{\"items\":[],\"total\":0,\"pageIndex\":1,\"pageSize\":20}}")
            });
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            return _response.Task;
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
