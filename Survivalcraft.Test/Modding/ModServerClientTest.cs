using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;

using Game.Modding;

namespace Survivalcraft.Test.Modding;

public sealed class ModServerClientTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"scnet-modrepo-{Guid.NewGuid():N}");

    [Fact]
    public void ClientParsesVersionResponseAndDownloadsIntoLocalRepository()
    {
        Directory.CreateDirectory(_root);
        var packageBytes = CreatePackageBytes("example.test", "1.0.0");
        var packageHash = LocalModRepository.ComputePackageHash(packageBytes, "example.test.1.0.0.scpak");
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri == "https://mods.example/api/v1/mods/example.test/versions/1.0.0")
            {
                return CreateJsonResponse(new
                {
                    success = true,
                    message = string.Empty,
                    code = 200,
                    data = new ModRepositoryPackage
                    {
                        ModId = "example.test",
                        Version = "1.0.0",
                        PackageHash = packageHash,
                        FileName = "example.test.1.0.0.scpak",
                        DownloadUrl = $"https://mods.example/api/v1/packages/{packageHash}"
                    }
                });
            }

            if (request.RequestUri.AbsoluteUri == $"https://mods.example/api/v1/packages/{packageHash}")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(packageBytes)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));
        using var client = new ModServerClient("https://mods.example/", httpClient);
        var repository = new LocalModRepository(_root);

        var package = client.FindPackage("example.test", "1.0.0");
        var localEntry = client.DownloadPackage(package!, repository);

        Assert.Equal("example.test", package!.ModId);
        Assert.Equal("1.0.0", package.Version);
        Assert.Equal("example.test", localEntry.ModId);
        Assert.Equal("1.0.0", localEntry.Version);
        Assert.Equal(packageHash, localEntry.PackageHash);
        Assert.EndsWith(".scpak", localEntry.FileName);
    }

    [Fact]
    public void LocalModRepositoryIndexesValidPackagesOnly()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, "alpha.scpak"), CreatePackageBytes("example.alpha", "1.0.0"));
        File.WriteAllBytes(Path.Combine(_root, "notes.txt"), [1, 2, 3]);

        var repository = new LocalModRepository(_root);
        var entries = repository.ListAll();

        var entry = Assert.Single(entries);
        Assert.Equal("example.alpha", entry.ModId);
        Assert.Equal("1.0.0", entry.Version);
        Assert.Equal(LocalModRepository.ComputePackageHash(Path.Combine(_root, "alpha.scpak")), entry.PackageHash);
    }

    [Fact]
    public void ClientLoadsAllPagedRepositoryResults()
    {
        var packages = Enumerable.Range(1, 11)
            .Select(index => new ModRepositoryPackage
            {
                ModId = $"example.{index}",
                Version = "1.0.0",
                PackageHash = index.ToString("x64"),
                FileName = $"example.{index}.scpak",
                DownloadUrl = $"/api/v1/packages/{index:x64}"
            })
            .ToArray();
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            var pageIndex = request.RequestUri!.Query.Contains("pageIndex=2", StringComparison.Ordinal) ? 2 : 1;
            var items = pageIndex == 1 ? packages.Take(10) : packages.Skip(10);
            return CreateJsonResponse(new
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
            });
        }));
        using var client = new ModServerClient("https://mods.example", httpClient);

        var result = client.ListPackages();

        Assert.Equal(11, result.Count);
        Assert.Equal("example.1", result[0].ModId);
        Assert.Equal("example.11", result[10].ModId);
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
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var manifestEntry = archive.CreateEntry("manifest.json");
            using var writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8, leaveOpen: false);
            writer.Write($$"""
                           {
                             "id": "{{modId}}",
                             "name": "{{modId}}",
                             "version": "{{version}}"
                           }
                           """);
        }

        return stream.ToArray();
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
