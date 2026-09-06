using System.Net;
using System.Text;
using System.Text.Json;

using Game.Content;

namespace Survivalcraft.Test.Modding;

public sealed class ContentCatalogServiceTest
{
    [Fact]
    public async Task MergesIdenticalSourcesAndPreservesHashConflictsAndFailures()
    {
        var hashA = new string('a', 64);
        var hashB = new string('b', 64);
        var first = Repository("A", "https://a.example", 0);
        var second = Repository("B", "https://b.example", 1);
        var broken = Repository("C", "https://c.example", 2);
        var responses = new Dictionary<Guid, HttpResponseMessage>
        {
            [first.Id] = Page(Item("content-a", "version-a", "1.0.0", hashA), 1),
            [second.Id] = Page([
                Item("content-b", "version-b1", "1.0.0", hashA),
                Item("content-b", "version-b2", "1.0.0", hashB)
            ], 2),
            [broken.Id] = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        };
        using var pool = new ContentServerClientPool(new ResponseFactory(responses));
        var service = new ContentCatalogService(pool);

        var result = await service.QueryAsync(Guid.Empty, [first, second, broken],
            new ContentCatalogQuery(PageSize: 2));

        var entry = Assert.Single(result.Entries);
        Assert.Equal("example.mod", entry.Identifier);
        Assert.Equal(2, entry.Versions.Count);
        Assert.All(entry.Versions, version => Assert.True(version.HasHashConflict));
        Assert.Equal(2, entry.Versions.Single(version => version.PackageHash == hashA).Sources.Count);
        Assert.Single(entry.Versions.Single(version => version.PackageHash == hashB).Sources);
        Assert.Equal(broken.Id, Assert.Single(result.Failures).RepositoryId);
        Assert.False(result.HasMore);
    }

    [Fact]
    public async Task OrdersVersionsBySemanticVersionAndReportsPerRepositoryInvalidData()
    {
        var first = Repository("A", "https://a.example", 0);
        var invalid = Repository("B", "https://b.example", 1);
        var responses = new Dictionary<Guid, HttpResponseMessage>
        {
            [first.Id] = Page([
                Item("a", "v1", "1.0.0", new string('a', 64)),
                Item("a", "v2", "2.0.0-beta.1", new string('b', 64)),
                Item("a", "v3", "2.0.0", new string('c', 64))
            ], 4),
            [invalid.Id] = Page(Item("b", "bad", "not-semver", new string('d', 64)), 1)
        };
        using var pool = new ContentServerClientPool(new ResponseFactory(responses));
        var service = new ContentCatalogService(pool);

        var result = await service.QueryAsync(Guid.NewGuid(), [first, invalid],
            new ContentCatalogQuery(Type: "Mod", Search: "example value", PageSize: 3));

        Assert.Equal(["2.0.0", "2.0.0-beta.1", "1.0.0"],
            Assert.Single(result.Entries).Versions.Select(version => version.Version));
        Assert.Equal(invalid.Id, Assert.Single(result.Failures).RepositoryId);
        Assert.True(result.HasMore);
    }

    private static ContentRepository Repository(string name, string address, int priority)
    {
        return new ContentRepository { Name = name, BaseUrl = address, Priority = priority };
    }

    private static ContentCatalogItem Item(string contentId, string versionId, string version, string hash)
    {
        return new ContentCatalogItem
        {
            ContentId = contentId,
            PublisherId = "publisher",
            Type = "Mod",
            Identifier = "example.mod",
            Name = "Example",
            VersionId = versionId,
            Version = version,
            PackageHash = hash,
            PackageSize = 100,
            FileName = "example.scpkg",
            DownloadUrl = $"api/v1/packages/{hash}"
        };
    }

    private static HttpResponseMessage Page(ContentCatalogItem item, int total)
    {
        return Page([item], total);
    }

    private static HttpResponseMessage Page(IReadOnlyList<ContentCatalogItem> items, int total)
    {
        var body = new
        {
            success = true,
            message = string.Empty,
            code = 200,
            data = new { items, total, pageIndex = 1, pageSize = items.Count }
        };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
    }

    private sealed class ResponseFactory(IReadOnlyDictionary<Guid, HttpResponseMessage> responses)
        : ContentServerClientFactory
    {
        public override ContentServerClient Create(ContentRepository repository)
        {
            var handler = new StubHttpMessageHandler(() => responses[repository.Id]);
            return new ContentServerClient(repository.BaseUrl, new HttpClient(handler), true);
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(response());
        }
    }
}
