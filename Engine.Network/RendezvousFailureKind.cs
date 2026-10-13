namespace Engine.Network;

public enum RendezvousFailureKind
{
    Unreachable,
    Timeout,
    InvalidResponse,
    InvalidRequest,
    InvitationNotFound,
    InvitationFull,
    CapacityReached,
    Unauthorized,
    RelayUnavailable,
    UdpUnavailable,
    HolePunchUnavailable,
    RateLimited,
    Rejected
}
