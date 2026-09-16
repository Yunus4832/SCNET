using System.Net;
using System.Text;

using ServerSource.Protocol;

namespace ServerSource.Protocol.Test;

public sealed class ServerSourceProtocolClientTest
{
    [Fact]
    public async Task ClientReadsAndValidatesPage()
    {
        var handler = new StubHandler("""
            {
              "protocolVersion": 1,
              "source": { "id": "example", "name": "Example" },
              "servers": [
                {
                  "id": "server-1",
                  "name": "Server One",
                  "address": "example.org:28887",
                  "description": null,
                  "tags": []
                }
              ],
              "nextCursor": null
            }
            """);
        var client = new ServerSourceProtocolClient(new HttpClient(handler));

        var page = await client.GetPageAsync(new Uri("https://source.example/servers"), "next value", 20);

        Assert.Equal("server-1", Assert.Single(page.Servers).Id);
        Assert.Equal("?limit=20&cursor=next%20value", handler.RequestUri?.Query);
    }

    [Fact]
    public async Task ClientRejectsInvalidResponse()
    {
        var handler = new StubHandler("""
            {
              "protocolVersion": 99,
              "source": { "id": "example", "name": "Example" },
              "servers": [],
              "nextCursor": null
            }
            """);
        var client = new ServerSourceProtocolClient(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<ServerSourceProtocolException>(() =>
            client.GetPageAsync(new Uri("https://source.example/servers")));

        Assert.Contains(exception.ValidationIssues,
            issue => issue.Code == ServerSourceValidationCode.UnsupportedProtocolVersion);
    }

    [Fact]
    public async Task ClientReadsAllPagesAndRejectsRepeatedEntryIds()
    {
        var handler = new CallbackHandler(request => request.RequestUri?.Query.Contains("cursor=second") == true
            ? Page("server-1", null)
            : Page("server-1", "second"));
        var client = new ServerSourceProtocolClient(new HttpClient(handler));

        await Assert.ThrowsAsync<ServerSourceProtocolException>(() =>
            client.GetAllAsync(new Uri("https://source.example/servers")));
    }

    [Fact]
    public async Task ClientCombinesAllPages()
    {
        var handler = new CallbackHandler(request => request.RequestUri?.Query.Contains("cursor=second") == true
            ? Page("server-2", null)
            : Page("server-1", "second"));
        var client = new ServerSourceProtocolClient(new HttpClient(handler));

        var snapshot = await client.GetAllAsync(new Uri("https://source.example/servers"));

        Assert.Equal(["server-1", "server-2"], snapshot.Servers.Select(server => server.Id));
    }

    private static string Page(string entryId, string? nextCursor)
    {
        var cursor = nextCursor is null ? "null" : $"\"{nextCursor}\"";
        return $$"""
                   {
                     "protocolVersion": 1,
                     "source": { "id": "example", "name": "Example" },
                     "servers": [
                       {
                         "id": "{{entryId}}",
                         "name": "Server",
                         "address": "example.org:28887",
                         "description": null,
                         "tags": []
                       }
                     ],
                     "nextCursor": {{cursor}}
                   }
                   """;
    }

    private sealed class StubHandler(string responseBody) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class CallbackHandler(Func<HttpRequestMessage, string> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response(request), Encoding.UTF8, "application/json")
            });
        }
    }
}
