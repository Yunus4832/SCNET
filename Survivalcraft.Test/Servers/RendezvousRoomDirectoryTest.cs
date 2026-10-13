using System.Net;
using System.Net.Http.Json;

using Engine.Network;

using Game.Servers;

using Rendezvous.Protocol;

namespace Survivalcraft.Test.Servers;

public sealed class RendezvousRoomDirectoryTest
{
    [Fact]
    public async Task BrowsingOnlyReadsDescriptorAndPagesAndUsesSelectedServiceForJoining()
    {
        var first = new RoomSummary(Guid.NewGuid(), "First", "", "000123", ["holePunch", "relay"]);
        var second = new RoomSummary(Guid.NewGuid(), "Second", "", "000456", ["relay"]);
        using var handler = new DirectoryHandler(new RoomPage([first], "next"), new RoomPage([second], null));
        using var http = new HttpClient(handler);
        var service = new InstalledRendezvousService { Name = "Signal", ControlUrl = "https://signal.example/" };
        var rooms = await RendezvousRoomDirectory.LoadAsync(http, service, CancellationToken.None);
        Assert.Equal(2, rooms.Count);
        Assert.Equal(["/transport", "/rooms?limit=100", "/rooms?limit=100&cursor=next"], handler.Requests);
        var target = rooms[0].CreateTarget(RelayPolicy.Required);
        Assert.Equal(new InvitationCodeNetworkTarget(new Uri(service.ControlUrl), "000123", RelayPolicy.Required), target);
        Assert.Equal("Signal", rooms[0].ServiceName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectsRepeatedIdentityOrCursorInsteadOfLooping(bool repeatedIdentity)
    {
        var first = new RoomSummary(Guid.NewGuid(), "First", "", "123456", ["relay"]);
        var second = first with { Id = repeatedIdentity ? first.Id : Guid.NewGuid() };
        using var handler = new DirectoryHandler(new RoomPage([first], "next"), new RoomPage([second], "next"));
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => RendezvousRoomDirectory.LoadAsync(http,
            new InstalledRendezvousService { ControlUrl = "https://signal.example/" }, CancellationToken.None));
        Assert.Equal(3, handler.Requests.Count);
    }

    private sealed class DirectoryHandler(params RoomPage[] pages) : HttpMessageHandler
    {
        private readonly Queue<RoomPage> _pages = new(pages);
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Requests.Add(request.RequestUri!.PathAndQuery);
            var response = request.RequestUri.AbsolutePath == "/transport"
                ? JsonContent.Create(new RendezvousDescriptor(9521, null, "rooms"))
                : JsonContent.Create(_pages.Dequeue());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = response });
        }
    }
}
