using Engine.Network;

namespace Game.Network.Transport;

/// <summary>数据包的传输方式声明。</summary>
public enum TransportMode
{
    /// <summary>可靠传输：不可丢、按序到达。</summary>
    Reliable,

    /// <summary>快照传输：可丢弃、单数据报，最新优先由传输层保证。</summary>
    Snapshot,

    /// <summary>状态流传输：可丢弃、多数据报，最新优先由应用层 StateTick 保证。</summary>
    StateStream,

    /// <summary>尽力而为的一次性效果：瞬时事件，丢失即消失，无最新优先语义。</summary>
    Effect
}

public readonly record struct PackageTransport(
    NetworkChannel Channel,
    DeliveryMode Delivery,
    double FlushInterval,
    bool Coalesce = false
)
{
    public byte ChannelNumber => (byte)Channel;

    /// <summary>传输方式语义分类，由通道与投递方式推导。</summary>
    public TransportMode Mode => Delivery switch
    {
        DeliveryMode.Sequenced => TransportMode.Snapshot,
        DeliveryMode.Unreliable =>
            Channel == NetworkChannel.StateStream ? TransportMode.StateStream : TransportMode.Effect,
        _ => TransportMode.Reliable
    };
}
