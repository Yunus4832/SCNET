using System.Net;
using System.Text;
using System.Text.Json;

using Content.Packaging;

using Game.Content;
using Game.Modding;

namespace Survivalcraft.Test.Modding;

public sealed class ContentServerClientModTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"scnet-modrepo-{Guid.NewGuid():N}");

    [Fact]
    public async Task DownloadServiceParsesVersionResponseAndDownloadsIntoLocalRepository()
    {
        Directory.CreateDirectory(_root);
        var packageBytes = CreatePackageBytes("example.test", "1.0.0");
        using var packageStream = new MemoryStream(packageBytes, writable: false);
        var packageHash = LocalModRepository.ComputePackageHash(packageStream, "example.test.1.0.0.scpkg");
        using var httpClient = new HttpClient(
            new StubHttpMessageHandler(request =>
                {
                    if (request.RequestUri!.AbsoluteUri ==
                        "https://mods.example/api/v1/mods/example.test/versions/1.0.0")
                    {
                        return CreateJsonResponse(
                            new
                            {
                                success = true,
                                message = string.Empty,
                                code = 200,
                                data = new ContentServerModPackage
                                {
                                    ModId = "example.test",
                                    Version = "1.0.0",
                                    PackageHash = packageHash,
                                    FileName = "example.test.1.0.0.scpkg",
                                    DownloadUrl = $"https://mods.example/api/v1/packages/{packageHash}"
                                }
                            }
                        );
                    }

                    if (request.RequestUri.AbsoluteUri == $"https://mods.example/api/v1/packages/{packageHash}")
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent(packageBytes)
                        };
                    }

                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }
            )
        );
        var source = new ContentRepository { Name = "Mods", BaseUrl = "https://mods.example/" };
        using var pool = new ContentServerClientPool(new TestClientFactory(httpClient));
        var cache = new ContentPackageCache(_root);
        var service = new ContentDownloadService(pool, cache);
        var context = ContentSourceContext.Persistent([source]);
        var result = await service.DownloadExactModAsync(context, "example.test", "1.0.0", packageHash);
        var repository = new LocalModRepository(_root);
        var localEntry = Assert.Single(repository.ListAll());

        Assert.False(result.WasCached);
        Assert.Equal(new ContentSourceId(Guid.Empty, source.Id), result.Source);
        Assert.Equal("example.test", localEntry.ModId);
        Assert.Equal("1.0.0", localEntry.Version);
        Assert.Equal(packageHash, localEntry.PackageHash);
        Assert.EndsWith(".scpkg", localEntry.FileName);
    }

    [Fact]
    public void LocalModRepositoryIndexesValidPackagesOnly()
    {
        Directory.CreateDirectory(_root);
        var sourcePath = Path.Combine(Path.GetTempPath(), $"alpha-{Guid.NewGuid():N}.scpkg");
        File.WriteAllBytes(sourcePath, CreatePackageBytes("example.alpha", "1.0.0"));
        File.WriteAllBytes(Path.Combine(_root, "notes.txt"), [1, 2, 3]);

        var repository = new LocalModRepository(_root);
        var imported = repository.ImportPackage(sourcePath);
        File.Delete(sourcePath);
        var entries = repository.ListAll();

        var entry = Assert.Single(entries);
        Assert.Equal("example.alpha", entry.ModId);
        Assert.Equal("1.0.0", entry.Version);
        Assert.Equal(imported.PackageHash, entry.PackageHash);
    }

    [Fact]
    public void LocalModRepositoryKeepsMultipleVersionsWithoutSelectingEither()
    {
        Directory.CreateDirectory(_root);
        var repository = new LocalModRepository(_root);
        using var first = new MemoryStream(CreatePackageBytes("example.multi", "1.0.0"), writable: false);
        using var second = new MemoryStream(CreatePackageBytes("example.multi", "2.0.0"), writable: false);

        repository.AddPackage(first);
        repository.AddPackage(second);

        var versions = repository.ListAll().Where(entry => entry.ModId == "example.multi")
            .Select(entry => entry.Version).OrderBy(version => version, StringComparer.Ordinal).ToArray();
        Assert.Equal(["1.0.0", "2.0.0"], versions);
    }

    [Fact]
    public async Task LocalModRepositoryRefreshesAfterExternalCacheWrite()
    {
        Directory.CreateDirectory(_root);
        var packageBytes = CreatePackageBytes("example.downloaded", "1.0.0");
        using var inspectionStream = new MemoryStream(packageBytes, writable: false);
        var packageHash = LocalModRepository.ComputePackageHash(inspectionStream, "example.downloaded.scpkg");
        var requirement = new ModPackageRequirement
        {
            ModId = "example.downloaded",
            Version = "1.0.0",
            PackageHash = packageHash
        };
        var repository = new LocalModRepository(_root);
        Assert.Null(repository.Find(requirement));

        var cache = new ContentPackageCache(_root);
        using var packageStream = new MemoryStream(packageBytes, writable: false);
        await cache.ImportExactAsync(
            packageStream,
            ContentPackageType.Mod,
            requirement.ModId,
            requirement.Version,
            requirement.PackageHash
        );

        Assert.Null(repository.Find(requirement));
        repository.Invalidate();
        Assert.NotNull(repository.Find(requirement));
    }

    [Fact]
    public async Task ClientLoadsCurrentCatalogPages()
    {
        var packages = Enumerable.Range(1, 11)
            .Select(index => new ContentCatalogItem
                {
                    Identifier = $"example.{index}",
                    Type = "Mod",
                    Version = "1.0.0",
                    PackageHash = index.ToString("x64"),
                    FileName = $"example.{index}.scpkg",
                    DownloadUrl = $"/api/v1/packages/{index:x64}"
                }
            )
            .ToArray();
        using var httpClient = new HttpClient(
            new StubHttpMessageHandler(request =>
                {
                    var pageIndex = request.RequestUri!.Query.Contains("pageIndex=2", StringComparison.Ordinal) ? 2 : 1;
                    var items = pageIndex == 1 ? packages.Take(10) : packages.Skip(10);
                    return CreateJsonResponse(
                        new
                        {
                            success = true,
                            message = string.Empty,
                            code = 200,
                            data = new
                            {
                                items,
                                total = packages.Length,
                                pageIndex,
                                pageSize = 10
                            }
                        }
                    );
                }
            )
        );
        using var client = new ContentServerClient("https://mods.example", httpClient);

        var first = await client.ListPageAsync(new ContentCatalogQuery(Type: "Mod", PageSize: 10));
        var second = await client.ListPageAsync(new ContentCatalogQuery(Type: "Mod", PageIndex: 2, PageSize: 10));

        Assert.Equal(11, first.Total);
        Assert.Equal(10, first.Items.Count);
        Assert.Equal("example.1", first.Items[0].Identifier);
        Assert.Equal("example.11", Assert.Single(second.Items).Identifier);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private static HttpResponseMessage CreateJsonResponse<T>(T value)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
        };
    }

    private static byte[] CreatePackageBytes(string modId, string version)
    {
        using var stream = ScpkgTestPackage.Create(
            $$"""
              {
                "id": "{{modId}}",
                "name": "{{modId}}",
                "version": "{{version}}"
              }
              """,
            new Dictionary<string, string> { ["data/marker.txt"] = "data" }
        );
        return stream.ToArray();
    }

    private sealed class TestClientFactory(HttpClient httpClient) : ContentServerClientFactory
    {
        public override ContentServerClient Create(ContentRepository repository)
        {
            return new ContentServerClient(repository.BaseUrl, httpClient);
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(handler(request));
        }
    }
}
