using LiteNetLib;

namespace Game.Network;

/// <summary>
///     网络层有四种传输方式，通道按模式严格区分：
///     1. 可靠传输（Control/ReliableEvent/Bulk）：不可丢、按序到达；
///     2. 快照传输（Snapshot）：可丢弃、单数据报，最新优先由传输层 Sequenced 保证；
///     3. 状态流传输（StateStream）：可丢弃、多数据报，最新优先由应用层 StateTick 保证；
///     4. 效果传输（Effect）：尽力而为的一次性效果（音效/特效等瞬时事件），
///     丢失即消失，没有最新优先语义。
/// </summary>
public enum NetworkChannel : byte
{
    /// <summary>可靠有序：生命周期、命令等必须到达的数据。</summary>
    Control = 0,

    /// <summary>可靠有序：事件型数据。</summary>
    ReliableEvent = 1,

    /// <summary>快照：可丢弃、单数据报，最新优先由传输层 Sequenced 保证。</summary>
    Snapshot = 2,

    /// <summary>尽力而为的一次性效果：瞬时事件，丢失即消失，无最新优先语义。</summary>
    Effect = 3,

    /// <summary>可靠有序：实体加载、地形等大块数据。</summary>
    Bulk = 4,

    /// <summary>状态流：可丢弃、多数据报，最新优先由应用层 StateTick 保证。</summary>
    StateStream = 5,

    /// <summary>可靠无序：地形区块独立到达，避免一个丢失分片阻塞其他区块。</summary>
    TerrainBulk = 6,

    /// <summary>应用层地形分片：单数据报、丢失后由区块请求重试补齐。</summary>
    TerrainFragment = 7
}

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
    DeliveryMethod DeliveryMethod,
    double FlushInterval,
    bool Coalesce = false
)
{
    public byte ChannelNumber => (byte)Channel;

    /// <summary>传输方式语义分类，由通道与投递方式推导。</summary>
    public TransportMode Mode => DeliveryMethod switch
    {
        DeliveryMethod.Sequenced => TransportMode.Snapshot,
        DeliveryMethod.Unreliable =>
            Channel == NetworkChannel.StateStream ? TransportMode.StateStream : TransportMode.Effect,
        _ => TransportMode.Reliable
    };
}
