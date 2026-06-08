using EntitySystem.Core;

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class EntityPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var el = new List<Entity>();
        switch (Type)
        {
            case EventType.LoadOne:
            case EventType.LoadList:
                foreach (var e in Entities)
                    //如果本地ID没有重复的添加，重复了进行替换
                {
                    if (!project.FindEntityById(e.EntityId, e2 =>
                        {
                            project.RemoveEntity(e2, true);
                            project.AddEntity(e);
                        }))
                    {
                        el.Add(e);
                    }
                }

                project.AddEntities(el);
                break;
            case EventType.Remove:
                project.FindEntityById(EntityId, entity => { project.RemoveEntity(entity, true); });
                break;
            case EventType.RequestSync:
                foreach (var e in EntityIdList)
                {
                    project.FindEntityById(e, entity =>
                    {
                        if (ShouldSendEntityToClients(entity))
                        {
                            el.Add(entity);
                        }
                    });
                }

                netNode.QueuePackage(new EntityPackage(el) { To = From });
                break;
        }
    }
}

public sealed class EntityPackageHandler : PackageHandlerBase<EntityPackage>
{
    public override void Handle(EntityPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(EntityPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
