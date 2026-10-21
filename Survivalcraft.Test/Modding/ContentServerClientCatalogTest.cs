using System.Net;
using System.Text;
using System.Text.Json;

using Game.Content;

namespace Survivalcraft.Test.Modding;

public sealed class ContentServerClientCatalogTest
{
    [Theory]
    [InlineData("https://signal.example/", true)]
    [InlineData("http://signal.example/", false)]
    [InlineData("https://user:password@signal.example/", false)]
    public async Task SignalingDirectoryValidatesPublicControlAddresses(string address, bool accepted)
    {
        Uri? requested = null;
        using var http = new HttpClient(
            new StubHandler(request =>
                {
                    requested = request.RequestUri;
                    var body = new
                    {
                        success = true,
                        data = new
                        {
                            items = new[] { new ContentServerRendezvousService("service", "Signal", address, null) },
                            total = 1,
                            pageIndex = 1, pageSize = 100
                        }
                    };
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
                    };
                }
            )
        );
        using var client = new ContentServerClient("https://content.example/base", http);
        if (accepted)
        {
            Assert.Equal(address, Assert.Single(await client.ListRendezvousServicesAsync()).ControlUrl);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => client.ListRendezvousServicesAsync());
        }

        Assert.Equal(
            "https://content.example/base/api/v1/rendezvous-services?pageIndex=1&pageSize=100",
            requested?.AbsoluteUri
        );
    }

    [Fact]
    public async Task SignalingDirectoryReadsEveryBoundedPage()
    {
        var requests = new List<string>();
        using var http = new HttpClient(
            new StubHandler(request =>
                {
                    requests.Add(request.RequestUri!.Query);
                    var second = requests.Count == 2;
                    var items = Enumerable.Range(second ? 100 : 0, second ? 1 : 100)
                        .Select(index => new ContentServerRendezvousService(
                                index.ToString(),
                                $"Signal {index}",
                                $"https://signal{index}.example/",
                                null
                            )
                        ).ToArray();
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            JsonSerializer.Serialize(
                                new
                                {
                                    success = true,
                                    data = new { items, total = 101, pageIndex = second ? 2 : 1, pageSize = 100 }
                                }
                            ),
                            Encoding.UTF8,
                            "application/json"
                        )
                    };
                }
            )
        );
        using var client = new ContentServerClient("https://content.example/", http);
        Assert.Equal(101, (await client.ListRendezvousServicesAsync()).Count);
        Assert.Equal(["?pageIndex=1&pageSize=100", "?pageIndex=2&pageSize=100"], requests);
    }

    [Fact]
    public async Task CatalogQueryEscapesFiltersAndUsesBoundedPage()
    {
        Uri? requested = null;
        using var httpClient = new HttpClient(
            new StubHandler(request =>
                {
                    requested = request.RequestUri;
                    return Page([], 0, 2, 25);
                }
            )
        );
        using var client = new ContentServerClient("https://content.example/base", httpClient);

        var page = await client.ListPageAsync(new ContentCatalogQuery("Mod", "name & value", 2, 25));

        Assert.Empty(page.Items);
        Assert.Equal(
            "https://content.example/base/api/v1/content?pageIndex=2&pageSize=25&type=Mod&query=name%20%26%20value",
            requested?.AbsoluteUri
        );
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ListPageAsync(new ContentCatalogQuery(PageSize: 101))
        );
    }

    [Fact]
    public async Task FindsExactVersionAndHashInVersionHistory()
    {
        var hash = new string('a', 64);
        Uri? requested = null;
        using var httpClient = new HttpClient(
            new StubHandler(request =>
                {
                    requested = request.RequestUri;
                    return Page(
                        [
                            Item("1.0.0", new string('b', 64)),
                            Item("1.0.0", hash)
                        ],
                        2,
                        1,
                        100
                    );
                }
            )
        );
        using var client = new ContentServerClient("https://content.example", httpClient);

        var item = await client.FindVersionAsync("publisher:item", "1.0.0", hash);

        Assert.Equal(hash, item?.PackageHash);
        Assert.Equal(
            "https://content.example/api/v1/content/publisher%3Aitem/versions?pageIndex=1&pageSize=100",
            requested?.AbsoluteUri
        );
    }

    [Fact]
    public async Task RejectsJsonResponseDeclaredAboveSizeLimit()
    {
        using var httpClient = new HttpClient(
            new StubHandler(_ =>
                {
                    var content = new StringContent("{}", Encoding.UTF8, "application/json");
                    content.Headers.ContentLength = ContentServerClient.MaximumJsonResponseBytes + 1;
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
                }
            )
        );
        using var client = new ContentServerClient("https://content.example", httpClient);

        await Assert.ThrowsAsync<InvalidDataException>(() => client.CheckHealthAsync());
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task HealthCheckReturnsHttpFailure(HttpStatusCode statusCode)
    {
        using var httpClient = new HttpClient(new StubHandler(_ => new HttpResponseMessage(statusCode)));
        using var client = new ContentServerClient("https://content.example", httpClient);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.CheckHealthAsync());

        Assert.Equal(statusCode, exception.StatusCode);
    }

    [Fact]
    public async Task HealthCheckReturnsUnreachableForNetworkFailure()
    {
        using var httpClient =
            new HttpClient(new StubHandler(_ => throw new HttpRequestException("Connection refused")));
        using var client = new ContentServerClient("https://content.example", httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.CheckHealthAsync());
    }

    [Fact]
    public async Task HealthCheckReturnsTimeoutForClientTimeout()
    {
        using var httpClient = new HttpClient(new StubHandler(_ => throw new TaskCanceledException("Timed out")));
        using var client = new ContentServerClient("https://content.example", httpClient);

        await Assert.ThrowsAsync<TaskCanceledException>(() => client.CheckHealthAsync());
    }

    [Fact]
    public async Task AnnouncementAndReleaseQueriesPropagateConnectionFailure()
    {
        using var httpClient =
            new HttpClient(new StubHandler(_ => throw new HttpRequestException("Connection refused")));
        using var client = new ContentServerClient("https://content.example", httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ListGameAnnouncementsAsync());
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetLatestGameReleaseAsync("linux"));
    }

    [Fact]
    public async Task MissingLatestReleaseIsSuccessfulEmptyResult()
    {
        using var httpClient = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var client = new ContentServerClient("https://content.example", httpClient);

        Assert.Null(await client.GetLatestGameReleaseAsync("linux"));
    }

    [Fact]
    public async Task ReadsRegisteredServerSources()
    {
        Uri? requested = null;
        using var httpClient = new HttpClient(
            new StubHandler(request =>
                {
                    requested = request.RequestUri;
                    var body = new
                    {
                        success = true,
                        message = string.Empty,
                        code = 200,
                        data = new[]
                        {
                            new
                            {
                                id = "source-1", name = "Public", apiUrl = "https://source.example/list",
                                description = "A source"
                            }
                        }
                    };
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
                    };
                }
            )
        );
        using var client = new ContentServerClient("https://content.example", httpClient);

        var sources = await client.ListServerSourcesAsync();

        Assert.Equal("https://content.example/api/v1/server-sources", requested?.AbsoluteUri);
        Assert.Equal("https://source.example/list", Assert.Single(sources).ApiUrl);
    }

    private static ContentCatalogItem Item(string version, string hash)
    {
        return new ContentCatalogItem { Version = version, PackageHash = hash };
    }

    private static HttpResponseMessage Page(
        IReadOnlyList<ContentCatalogItem> items,
        int total,
        int pageIndex,
        int pageSize
    )
    {
        var body = new
        {
            success = true,
            message = string.Empty,
            code = 200,
            data = new { items, total, pageIndex, pageSize }
        };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
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
