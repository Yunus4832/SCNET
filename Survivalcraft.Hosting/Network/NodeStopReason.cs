using Engine.Network;

namespace Game.Network;

/// <summary>Retains localized application text and the original transport fact until display.</summary>
public sealed record NodeStopReason(LocalizedText Message, NetworkDisconnectReason? TransportReason = null)
{
    public NodeStopReason(string message, NetworkDisconnectReason? transportReason = null)
        : this(LocalizedText.Literal(message), transportReason)
    {
    }

    public static NodeStopReason None { get; } = new(LocalizedText.Empty);
}
