using Engine.Network.Rendezvous;

using Rendezvous.Protocol;

namespace Game.Servers;

public enum P2PRoomStartFailure
{
    None = 0, ListenerRequired = 1, WorldRequired = 2, Stopping = 3, ServiceRequired = 4, InvalidConfiguration = 5
}

public static class P2PRoomConfiguration
{
    public static P2PRoomStartFailure Validate(P2PRoomSettings current, out P2PRoomSettings normalized)
    {
        normalized = current;
        if (string.IsNullOrWhiteSpace(current.ControlUrl)) { return P2PRoomStartFailure.ServiceRequired; }

        try
        {
            normalized = current with
            {
                ControlUrl = RendezvousAddress.Normalize(new Uri(current.ControlUrl)).AbsoluteUri
            };
            return new RoomPublication(normalized.Name, normalized.Description, normalized.IsPublic).IsValid
                ? P2PRoomStartFailure.None
                : P2PRoomStartFailure.InvalidConfiguration;
        }
        catch (Exception error) when (error is ArgumentException or UriFormatException)
        {
            return P2PRoomStartFailure.InvalidConfiguration;
        }
    }
}
