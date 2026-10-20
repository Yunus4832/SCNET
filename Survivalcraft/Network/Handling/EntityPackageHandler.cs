using EntitySystem.Core;

using Game.Network.Replication;
using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class EntityPackageHandler : PackageHandlerBase<EntityPackage>
{
    public override void Handle(EntityPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(EntityPackage)}");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        // 实体生命周期由服务端维护，客户端只能请求同步已有实体。
        if (isServer && package.Type != EntityPackage.EventType.RequestSync)
        {
            return;
        }

        var project = GameManager.Project;
        var el = new List<Entity>();
        switch (package.Type)
        {
            case EntityPackage.EventType.LoadOne:
            case EntityPackage.EventType.LoadList:
                foreach (var snapshot in package.Players)
                {
                    if (snapshot.Client is { } client && !netNode.Clients.ContainsKey(client.ID))
                    {
                        PackageDispatcher.Handle(new ClientPackage(client.ID, client.TokenId, client.GUID), context);
                    }
                }

                var players = project.FindSubsystem<SubsystemPlayers>(true)!;
                foreach (var snapshot in package.Players)
                {
                    var values = new EntitySystem.TemplatesDatabase.ValuesDictionary();
                    values.ApplyOverridesUseMessagePack(snapshot.Data);
                    var guid = values.GetValue("PlayerGUID", Guid.Empty);
                    if (players.PlayersData.All(player => player.PlayerGUID != guid))
                    {
                        var player = new PlayerData(project);
                        player.Load(values);
                        players.AddPlayerData(player);
                    }
                }

                project.AddEntities(EntitySnapshotSerializer.Deserialize(project, package.EntityData));
                break;
            case EntityPackage.EventType.Remove:
                project.FindEntityById(package.EntityId, entity => { project.RemoveEntity(entity, true); });
                break;
            case EntityPackage.EventType.RequestSync:
                if (!isServer || context.Sender is null)
                {
                    break;
                }

                var interest = project.FindSubsystem<SubsystemNetworkInterest>(true)!;
                foreach (var e in package.EntityIdList)
                {
                    project.FindEntityById(e, entity =>
                    {
                        if (EntityPackageBuilder.ShouldSend(entity) &&
                            interest.ShouldIncludeInInitialSnapshot(context.Sender, entity))
                        {
                            var body = entity.FindComponent<ComponentBody>();
                            if (body is null)
                            {
                                el.Add(entity);
                                if (entity.FindComponent<ComponentBlockEntity>() is not null)
                                {
                                    interest.Entities.AddObserved(context.Sender,
                                        EntityInterestGroup.BlockEntities, entity.EntityId);
                                }
                                return;
                            }

                            foreach (var member in SubsystemNetworkInterest.GetBodyGroup(body))
                            {
                                el.Add(member.Entity);
                                interest.Entities.AddObserved(
                                    context.Sender,
                                    EntityInterestGroup.Creatures,
                                    member.Entity.EntityId);
                            }
                        }
                    });
                }

                if (context.Sender is not null)
                {
                    netNode.QueuePackage(
                        EntityPackageBuilder.Capture(project, el.Distinct().ToList()),
                        PackageAudience.To(context.Sender));
                }
                break;
        }
    }
}
