using Engine.Network;
using Engine.Network.Rendezvous;

using Rendezvous.Protocol;

namespace Game.Servers;

public static class RendezvousServiceProbe
{
    public static async Task<int> TestAsync(string address, CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var client = new RendezvousClient(http, RendezvousAddress.Normalize(new Uri(address)));
        var descriptor = await client.GetDescriptorAsync(cancellationToken).ConfigureAwait(false);
        if (descriptor.RoomDirectoryPath != "rooms" || descriptor.UdpPort is < 1 or > 65535)
        {
            throw new InvalidDataException("The signaling service does not support room discovery.");
        }

        var page = await client.GetRoomsAsync(null, RendezvousLimits.MaximumRoomPageSize, cancellationToken)
            .ConfigureAwait(false);
        return page.Rooms.Count;
    }
}
