using Engine.Network;

using Rendezvous.Protocol;

namespace Game.Servers;

public sealed record RendezvousRoom(Uri ControlUrl, string ServiceName, RoomSummary Summary)
{
    public InvitationCodeNetworkTarget CreateTarget(RelayPolicy relayPolicy) =>
        new(ControlUrl, Summary.JoinCode, relayPolicy);
}
