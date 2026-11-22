namespace Game.Terrains.Distribution;

/// <summary>描述地形实例负责生成权威内容，还是从已安装快照推导客户端数据；该职责独立于进程和网络模式。</summary>
public enum TerrainContentRole
{
    Authority = 0,

    Replica = 1
}
