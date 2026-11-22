using Engine.Network;

namespace Game.Network;

/// <summary>保留本地化应用文本及原始传输信息，直到显示时再处理。</summary>
public sealed record NodeStopReason(LocalizedText Message, NetworkDisconnectReason? TransportReason = null)
{
    public NodeStopReason(string message, NetworkDisconnectReason? transportReason = null)
        : this(LocalizedText.Literal(message), transportReason)
    {
    }

    public static NodeStopReason None { get; } = new(LocalizedText.Empty);
}
