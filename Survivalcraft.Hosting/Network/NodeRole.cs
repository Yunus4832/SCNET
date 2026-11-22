namespace Game.Network;

/// <summary>控制消息端点的启用，独立于传输连接状态和 GUI 能力。</summary>
public enum NodeRole
{
    Authority = 1,

    Client = 2,

    Inactive = 3
}
