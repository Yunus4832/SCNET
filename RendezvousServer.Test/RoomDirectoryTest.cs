using System.Net;

using Rendezvous.Protocol;

using RendezvousServer.Sessions;

namespace RendezvousServer.Test;

public sealed class RoomDirectoryTest
{
    [Fact]
    public void EndpointExpiryHidesRoomAndPublicationDoesNotExtendInvitationLease()
    {
        var clock = new ManualClock();
        var registry = new InvitationRegistry(clock, TimeSpan.FromSeconds(90), 10);
        var registration = registry.Create(1);
        var endpoints = new EndpointRegistrations(registry, clock, 60);
        var ticket = registry.IssueTicket(registration.SessionId, Guid.Empty, registration.HostCredential);
        var address = new IPEndPoint(IPAddress.Loopback, 41000);
        var challenge = endpoints.Challenge(registration.SessionId, Guid.Empty, ticket.Token, address);
        Assert.True(endpoints.Confirm(registration.SessionId, Guid.Empty, ticket.Token, challenge, address));
        registry.UpdateRoom(registration.SessionId, registration.HostCredential, new RoomPublication("Room", "", true));
        Assert.Single(registry.ListRooms(100, null).Rooms);
        clock.Advance(TimeSpan.FromSeconds(61));
        Assert.Empty(registry.ListRooms(100, null).Rooms);
        registry.UpdateRoom(registration.SessionId, registration.HostCredential, new RoomPublication("Changed", "", true));
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.False(registry.IsActive(registration.SessionId, Guid.Empty));
    }

    [Fact]
    public void OnlyPublicReachableRoomsAreVisibleAndReadingDoesNotOccupyCapacity()
    {
        var registry = new InvitationRegistry(TimeProvider.System, TimeSpan.FromMinutes(5), 10);
        var registration = registry.Create(1);
        registry.UpdateRoom(registration.SessionId, registration.HostCredential, new RoomPublication("Room", "", true));
        Assert.Empty(registry.ListRooms(100, null).Rooms);
        Confirm(registry, registration);
        Assert.Single(registry.ListRooms(100, null).Rooms);
        Assert.Single(registry.ListRooms(100, null).Rooms);
        registry.Join(registration.JoinCode, Guid.NewGuid());
        registry.UpdateRoom(registration.SessionId, registration.HostCredential, new RoomPublication("Room", "", false));
        Assert.Empty(registry.ListRooms(100, null).Rooms);
        Assert.Throws<InvalidOperationException>(() => registry.UpdateRoom(registration.SessionId, "wrong", new RoomPublication("Room", "", true)));
    }

    [Fact]
    public void PagingExcludesNewRegistrationsAndClosedRoomsAndRejectsRestartedCursor()
    {
        var registry = new InvitationRegistry(TimeProvider.System, TimeSpan.FromMinutes(5), 10);
        SessionRegistration Publish()
        {
            var registration = registry.Create(1);
            Confirm(registry, registration);
            registry.UpdateRoom(registration.SessionId, registration.HostCredential, new RoomPublication("Room", "", true));
            return registration;
        }
        var first = Publish();
        var second = Publish();
        var page = registry.ListRooms(1, null);
        Assert.Equal(first.SessionId, Assert.Single(page.Rooms).Id);
        Assert.NotNull(page.NextCursor);
        Publish();
        Assert.Equal(second.SessionId, Assert.Single(registry.ListRooms(1, page.NextCursor).Rooms).Id);
        registry.Close(second.SessionId, second.HostCredential);
        Assert.Empty(registry.ListRooms(1, page.NextCursor).Rooms);
        var restarted = new InvitationRegistry(TimeProvider.System, TimeSpan.FromMinutes(5), 10);
        Assert.Throws<ArgumentException>(() => restarted.ListRooms(1, page.NextCursor));
    }

    private static void Confirm(InvitationRegistry registry, SessionRegistration registration)
    {
        var endpoints = new EndpointRegistrations(registry, TimeProvider.System);
        var ticket = registry.IssueTicket(registration.SessionId, Guid.Empty, registration.HostCredential);
        var address = new IPEndPoint(IPAddress.Loopback, 41000);
        var challenge = endpoints.Challenge(registration.SessionId, Guid.Empty, ticket.Token, address);
        Assert.True(endpoints.Confirm(registration.SessionId, Guid.Empty, ticket.Token, challenge, address));
    }
    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }

}
