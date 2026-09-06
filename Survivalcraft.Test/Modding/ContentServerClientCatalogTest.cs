using System.Net;
using System.Text;
using System.Text.Json;

using Game.Content;

namespace Survivalcraft.Test.Modding;

public sealed class ContentServerClientCatalogTest
{
    [Fact]
    public async Task CatalogQueryEscapesFiltersAndUsesBoundedPage()
    {
        Uri? requested = null;
        using var httpClient = new HttpClient(new StubHandler(request =>
        {
            requested = request.RequestUri;
            return Page([], 0, 2, 25);
        }));
        using var client = new ContentServerClient("https://content.example/base", httpClient);

        var page = await client.ListPageAsync(new ContentCatalogQuery("Mod", "name & value", 2, 25));

        Assert.Empty(page.Items);
        Assert.Equal(
            "https://content.example/base/api/v1/content?pageIndex=2&pageSize=25&type=Mod&query=name%20%26%20value",
            requested?.AbsoluteUri);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ListPageAsync(new ContentCatalogQuery(PageSize: 101)));
    }

    [Fact]
    public async Task FindsExactVersionAndHashInVersionHistory()
    {
        var hash = new string('a', 64);
        Uri? requested = null;
        using var httpClient = new HttpClient(new StubHandler(request =>
        {
            requested = request.RequestUri;
            return Page([
                Item("1.0.0", new string('b', 64)),
                Item("1.0.0", hash)
            ], 2, 1, 100);
        }));
        using var client = new ContentServerClient("https://content.example", httpClient);

        var item = await client.FindVersionAsync("publisher:item", "1.0.0", hash);

        Assert.Equal(hash, item?.PackageHash);
        Assert.Equal(
            "https://content.example/api/v1/content/publisher%3Aitem/versions?pageIndex=1&pageSize=100",
            requested?.AbsoluteUri);
    }

    [Fact]
    public async Task RejectsJsonResponseDeclaredAboveSizeLimit()
    {
        using var httpClient = new HttpClient(new StubHandler(_ =>
        {
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            content.Headers.ContentLength = ContentServerClient.MaximumJsonResponseBytes + 1;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }));
        using var client = new ContentServerClient("https://content.example", httpClient);

        await Assert.ThrowsAsync<InvalidDataException>(() => client.CheckHealthAsync());
    }

    private static ContentCatalogItem Item(string version, string hash)
    {
        return new ContentCatalogItem { Version = version, PackageHash = hash };
    }

    private static HttpResponseMessage Page(IReadOnlyList<ContentCatalogItem> items, int total, int pageIndex,
        int pageSize)
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
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
