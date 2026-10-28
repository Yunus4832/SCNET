using Engine.Network.Rendezvous;

using Rendezvous.Protocol;

namespace Game.Servers;

public static class RendezvousRoomDirectory
{
    public static async Task<IReadOnlyList<RendezvousRoom>> LoadAsync(
        HttpClient http,
        InstalledRendezvousService service,
        CancellationToken cancellationToken
    )
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        cancellationToken = deadline.Token;
        var address = RendezvousAddress.Normalize(new Uri(service.ControlUrl));
        var client = new RendezvousClient(http, address);
        var descriptor = await client.GetDescriptorAsync(cancellationToken).ConfigureAwait(false);
        if (descriptor.RoomDirectoryPath != "rooms")
        {
            throw new InvalidDataException("The signaling service does not support room discovery.");
        }

        var rooms = new List<RendezvousRoom>();
        var identities = new HashSet<Guid>();
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        for (var pageIndex = 0; pageIndex < RendezvousLimits.MaximumRoomPages; pageIndex++)
        {
            var page = await client.GetRoomsAsync(cursor, RendezvousLimits.MaximumRoomPageSize, cancellationToken)
                .ConfigureAwait(false);
            foreach (var room in page.Rooms)
            {
                if (!identities.Add(room.Id))
                {
                    throw new InvalidDataException("The room directory contains duplicate identities.");
                }

                rooms.Add(new RendezvousRoom(address, service.Name, room));
            }

            if (page.NextCursor is null)
            {
                return rooms;
            }

            if (!cursors.Add(page.NextCursor))
            {
                throw new InvalidDataException("The room directory repeats a pagination cursor.");
            }

            cursor = page.NextCursor;
        }

        throw new InvalidDataException("The room directory exceeds the pagination limit.");
    }
}
