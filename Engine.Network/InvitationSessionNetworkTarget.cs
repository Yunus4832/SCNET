namespace Engine.Network;

public sealed record InvitationSessionNetworkTarget : NetworkTarget
{
    public InvitationSessionNetworkTarget(Uri controlAddress, Guid sessionId, RelayPolicy relayPolicy = RelayPolicy.Allowed)
    {
        ControlAddress = RendezvousAddress.Normalize(controlAddress);
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Expected an invitation session identifier.", nameof(sessionId));
        }
        if (!Enum.IsDefined(relayPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(relayPolicy));
        }
        SessionId = sessionId;
        RelayPolicy = relayPolicy;
    }

    public Uri ControlAddress { get; }

    public Guid SessionId { get; }

    public RelayPolicy RelayPolicy { get; }
}
