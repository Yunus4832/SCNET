namespace Game.World;

/// <summary>指定由哪个世界执行可更新对象的逐帧任务。</summary>
public enum UpdateExecutionSide
{
    Disabled = 0,

    Authority = 1,

    Client = 2,

    Both = 3,

    Presentation = 4
}
