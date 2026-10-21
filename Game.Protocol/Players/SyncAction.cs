namespace Game.Protocol.Players;

/// <summary>
/// 同步持续操作的生命周期；Start 状态下接收端可以逐帧推进操作。
/// </summary>
public enum SyncAction : byte
{
    None = 0,

    Start = 1,

    Cancel = 2,

    Complete = 3
}
