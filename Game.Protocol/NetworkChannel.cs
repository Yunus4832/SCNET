namespace Game.Protocol;

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

    /// <summary>可靠有序：实体加载及需要原子应用的整列表数据。</summary>
    Bulk = 4,

    /// <summary>状态流：可丢弃、多数据报，最新优先由应用层 StateTick 保证。</summary>
    StateStream = 5,

    /// <summary>应用层地形分片：单数据报、丢失后由区块请求重试补齐。</summary>
    TerrainFragment = 6
}
