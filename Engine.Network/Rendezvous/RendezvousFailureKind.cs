namespace Engine.Network.Rendezvous;

public enum RendezvousFailureKind
{
    Unreachable = 0,

    Timeout = 1,

    InvalidResponse = 2,

    InvalidRequest = 3,

    InvitationNotFound = 4,

    InvitationFull = 5,

    CapacityReached = 6,

    Unauthorized = 7,

    RelayUnavailable = 8,

    UdpUnavailable = 9,

    HolePunchUnavailable = 10,

    RateLimited = 11,

    Rejected = 12
}
