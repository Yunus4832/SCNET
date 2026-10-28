using Engine.Network.Rendezvous;

namespace Engine.Network;

public sealed record InvitationCodeNetworkTarget : NetworkTarget
{
    public InvitationCodeNetworkTarget(
        Uri controlAddress,
        string joinCode,
        RelayPolicy relayPolicy = RelayPolicy.Allowed
    )
    {
        ControlAddress = RendezvousAddress.Normalize(controlAddress);
        if (string.IsNullOrEmpty(joinCode) ||
            joinCode.Length != 6 ||
            joinCode.Any(character => character is < '0' or > '9'))
        {
            throw new ArgumentException("Expected a six-digit invitation code.", nameof(joinCode));
        }

        if (!Enum.IsDefined(relayPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(relayPolicy));
        }

        JoinCode = joinCode;
        RelayPolicy = relayPolicy;
    }

    public Uri ControlAddress { get; }

    public string JoinCode { get; }

    public RelayPolicy RelayPolicy { get; }
}
