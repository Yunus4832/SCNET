using EntitySystem.Core;

namespace Game.Network.Packages.Handlers;

public sealed class EntityPackageHandler : PackageHandlerBase<EntityPackage>
{
    public override void Handle(EntityPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(EntityPackage)}");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var el = new List<Entity>();
        switch (package.Type)
        {
            case EntityPackage.EventType.LoadOne:
            case EntityPackage.EventType.LoadList:
                // 如果本地ID没有重复的添加，重复了进行替换
                el.AddRange(package.Entities.Where(e => !project.FindEntityById(e.EntityId, e2 =>
                {
                    project.RemoveEntity(e2, true);
                    project.AddEntity(e);
                })));

                project.AddEntities(el);
                break;
            case EntityPackage.EventType.Remove:
                project.FindEntityById(package.EntityId, entity => { project.RemoveEntity(entity, true); });
                break;
            case EntityPackage.EventType.RequestSync:
                foreach (var e in package.EntityIdList)
                {
                    project.FindEntityById(e, entity =>
                    {
                        if (EntityPackage.ShouldSendEntityToClients(entity))
                        {
                            el.Add(entity);
                        }
                    });
                }

                netNode.QueuePackage(new EntityPackage(el) { To = package.From });
                break;
        }
    }
}
