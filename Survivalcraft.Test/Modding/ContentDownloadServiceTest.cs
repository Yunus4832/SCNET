using System.Net;

using Content.Packaging;

using Game.Content;

namespace Survivalcraft.Test.Modding;

public sealed class ContentDownloadServiceTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"scnet-download-{Guid.NewGuid():N}");

    [Fact]
    public async Task FallsBackForSameHashThenUsesCacheWithoutNetwork()
    {
        var package = CreatePackage("example.mod", "1.0.0");
        var hash = InspectHash(package);
        var first = new ContentRepository { Name = "A", BaseUrl = "https://a.example", Priority = 0 };
        var second = new ContentRepository { Name = "B", BaseUrl = "https://b.example", Priority = 1 };
        var firstHandler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var secondHandler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(package)
        });
        using var pool = CreatePool((first, firstHandler), (second, secondHandler));
        var cache = new ContentPackageCache(_root);
        var service = new ContentDownloadService(pool, cache);
        var (content, version) = CreateCatalog(first, second, hash);

        var downloaded = await service.DownloadAsync(content, version);
        var cached = await service.DownloadAsync(content, version);

        Assert.Equal(second.Id, downloaded.Source?.RepositoryId);
        Assert.Single(downloaded.PriorFailures);
        Assert.False(downloaded.WasCached);
        Assert.True(cached.WasCached);
        Assert.Null(cached.Source);
        Assert.Equal(1, firstHandler.RequestCount);
        Assert.Equal(1, secondHandler.RequestCount);
    }

    [Fact]
    public async Task ExplicitSourceDoesNotFallBackAndWrongPackageIsNotCached()
    {
        var expected = CreatePackage("example.mod", "1.0.0");
        var wrong = CreatePackage("other.mod", "1.0.0");
        var hash = InspectHash(expected);
        var first = new ContentRepository { Name = "A", BaseUrl = "https://a.example", Priority = 0 };
        var second = new ContentRepository { Name = "B", BaseUrl = "https://b.example", Priority = 1 };
        var firstHandler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(wrong)
        });
        var secondHandler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(expected)
        });
        using var pool = CreatePool((first, firstHandler), (second, secondHandler));
        var cache = new ContentPackageCache(_root);
        var service = new ContentDownloadService(pool, cache);
        var (content, version) = CreateCatalog(first, second, hash);

        var exception = await Assert.ThrowsAsync<ContentDownloadException>(() => service.DownloadAsync(
            content, version, new ContentSourceId(Guid.Empty, first.Id)));

        Assert.Single(exception.Failures);
        Assert.Null(cache.Find(hash));
        Assert.Empty(cache.List());
        Assert.Equal(1, firstHandler.RequestCount);
        Assert.Equal(0, secondHandler.RequestCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private static ContentServerClientPool CreatePool(
        params (ContentRepository Repository, CountingHandler Handler)[] sources)
    {
        var handlers = sources.ToDictionary(source => source.Repository.Id, source => source.Handler);
        var pool = new ContentServerClientPool(new HandlerFactory(handlers));
        pool.Update(Guid.Empty, sources.Select(source => source.Repository));
        return pool;
    }

    private static (AggregatedContentEntry Content, AggregatedContentVersion Version) CreateCatalog(
        ContentRepository first, ContentRepository second, string hash)
    {
        var sources = new[]
        {
            new ContentCatalogSource(Guid.Empty, first.Id, first.Name, first.Priority, "a", "v1",
                $"api/v1/packages/{hash}"),
            new ContentCatalogSource(Guid.Empty, second.Id, second.Name, second.Priority, "b", "v2",
                $"api/v1/packages/{hash}")
        };
        var version = new AggregatedContentVersion("1.0.0", hash, 100, "example.scpkg", false, sources);
        return (new AggregatedContentEntry(ContentPackageType.Mod, "example.mod", "Example", null, [version]),
            version);
    }

    private static byte[] CreatePackage(string identifier, string version)
    {
        using var stream = ScpkgTestPackage.Create($$"""
                                                     {
                                                       "id": "{{identifier}}",
                                                       "name": "{{identifier}}",
                                                       "version": "{{version}}"
                                                     }
                                                     """,
            new Dictionary<string, string> { ["data/marker.txt"] = "data" });
        return stream.ToArray();
    }

    private static string InspectHash(byte[] package)
    {
        using var stream = new MemoryStream(package, writable: false);
        return ContentPackageReader.Inspect(stream).PackageHash;
    }

    private sealed class HandlerFactory(IReadOnlyDictionary<Guid, CountingHandler> handlers)
        : ContentServerClientFactory
    {
        public override ContentServerClient Create(ContentRepository repository)
        {
            return new ContentServerClient(repository.BaseUrl, new HttpClient(handlers[repository.Id]), true);
        }
    }

    private sealed class CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(response(request));
        }
    }
}
