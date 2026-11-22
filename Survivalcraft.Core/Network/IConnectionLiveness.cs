namespace Game.Network;

/// <summary>反映实际传输连接的当前连通状态。</summary>
public interface IConnectionLiveness
{
    bool IsConnected { get; }
}
