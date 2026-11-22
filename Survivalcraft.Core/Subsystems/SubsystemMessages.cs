using EntitySystem.Core;

namespace Game.Subsystems;

/// <summary>管理单个世界的消息服务，供权威端路由与客户端表现订阅使用。</summary>
public sealed class SubsystemMessages : Subsystem
{
    public GameMessageService Service => field ??= new GameMessageService(Project);
}
