using Engine.Network;

namespace Game.Network;

/// <summary>Original application/remote text and optional transport fact, never localized by the node.</summary>
public sealed record NodeStopReason(string Message, NetworkDisconnectReason? TransportReason = null)
{
    public static NodeStopReason None { get; } = new(string.Empty);
}
