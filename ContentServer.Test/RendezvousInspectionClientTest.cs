using System.Net;
using System.Net.Http.Json;

using ContentServer.Application;

using Rendezvous.Protocol;

namespace ContentServer.Test;

public sealed class RendezvousInspectionClientTest
{
    [Theory]
    [InlineData(null)]
    [InlineData("https://other.example/rooms")]
    public async Task RejectsMissingOrForeignDirectory(string? path)
    {
        using var http = new HttpClient(new Handler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new RendezvousDescriptor(9521, null, path))
        }));
        using var client = new RendezvousInspectionClient(http);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.InspectAsync(new Uri("https://signal.example/"), CancellationToken.None));
    }

    [Fact]
    public async Task AcceptsBoundedDescriptorAndRejectsRedirectAndOversizedBody()
    {
        using (var http = new HttpClient(new Handler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new RendezvousDescriptor(9521, null, "rooms"))
        })))
        {
            using var client = new RendezvousInspectionClient(http);
            await client.InspectAsync(new Uri("https://signal.example/"), CancellationToken.None);
        }
        foreach (var response in new[]
                 {
                     new HttpResponseMessage(HttpStatusCode.Redirect),
                     new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 16385)) }
                 })
        {
            using var http = new HttpClient(new Handler(response));
            using var client = new RendezvousInspectionClient(http);
            await Assert.ThrowsAsync<HttpRequestException>(() => client.InspectAsync(new Uri("https://signal.example/"), CancellationToken.None));
        }
    }

    private sealed class Handler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
