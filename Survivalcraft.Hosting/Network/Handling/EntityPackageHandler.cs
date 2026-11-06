using EntitySystem.Core;

using Game.Network.Replication;

namespace Game.Network.Handling;

public sealed class EntityPackageHandler : PackageHandlerBase<EntityPackage>
{
    internal static bool AcceptsDirection(EntityPackage.EventType type, bool isAuthority) =>
        isAuthority ? type == EntityPackage.EventType.RequestSync : type != EntityPackage.EventType.RequestSync;

    public override void Handle(EntityPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(EntityPackage)}");
            return;
        }

        if (!ReferenceEquals(context.Runtime, netNode.Runtime) || context.Runtime.IsClosed || context.Project is null)
        {
            return;
        }

        // 实体生命周期由服务端维护，客户端只能请求同步已有实体。
        if (!AcceptsDirection(package.Type, isServer))
        {
            return;
        }

        if (!isServer && (context.Sender is null || !ReferenceEquals(context.Sender, netNode.Connections.Authority)))
        {
            return;
        }

        var project = context.Project;
        var el = new List<Entity>();
        switch (package)
        {
            case SingleEntitySnapshotPackage message1:
            {
                foreach (var snapshot in message1.Characters)
                {
                    if (snapshot.Controller is { } controller)
                    {
                        var identityValues = new EntitySystem.TemplatesDatabase.ValuesDictionary();
                        identityValues.ApplyOverridesUseMessagePack(snapshot.Data);
                        if (identityValues.GetValue<Guid>("CharacterId") != controller.CharacterId ||
                            !ClientPackageHandler.InstallController(netNode, controller)) { return; }
                    }
                }

                var players = project.FindSubsystem<SubsystemCharacters>(true)!;
                foreach (var snapshot in message1.Characters)
                {
                    var values = new EntitySystem.TemplatesDatabase.ValuesDictionary();
                    values.ApplyOverridesUseMessagePack(snapshot.Data);
                    var guid = values.GetValue("CharacterId", Guid.Empty);
                    var existing = players.Characters.FirstOrDefault(player => player.CharacterId == guid);
                    if (existing is null)
                    {
                        var player = new CharacterData(project);
                        player.Load(values);
                        players.AddCharacterData(player);
                    }
                    else { existing.ApplyReplicaState(values); }
                }

                project.AddEntities(EntitySnapshotSerializer.Deserialize(project, message1.EntityData));
                break;
            }
            case EntitySnapshotPackage message2:
            {
                foreach (var snapshot in message2.Characters)
                {
                    if (snapshot.Controller is { } controller)
                    {
                        var identityValues = new EntitySystem.TemplatesDatabase.ValuesDictionary();
                        identityValues.ApplyOverridesUseMessagePack(snapshot.Data);
                        if (identityValues.GetValue<Guid>("CharacterId") != controller.CharacterId ||
                            !ClientPackageHandler.InstallController(netNode, controller)) { return; }
                    }
                }

                var players = project.FindSubsystem<SubsystemCharacters>(true)!;
                foreach (var snapshot in message2.Characters)
                {
                    var values = new EntitySystem.TemplatesDatabase.ValuesDictionary();
                    values.ApplyOverridesUseMessagePack(snapshot.Data);
                    var guid = values.GetValue("CharacterId", Guid.Empty);
                    var existing = players.Characters.FirstOrDefault(player => player.CharacterId == guid);
                    if (existing is null)
                    {
                        var player = new CharacterData(project);
                        player.Load(values);
                        players.AddCharacterData(player);
                    }
                    else { existing.ApplyReplicaState(values); }
                }

                project.AddEntities(EntitySnapshotSerializer.Deserialize(project, message2.EntityData));
                break;
            }
            case EntityRemovedPackage message3:
            {
                project.FindEntityById(message3.EntityId, entity => { project.RemoveEntity(entity, true); });
                break;
            }
            case EntitySyncRequestPackage message4:
            {
                if (!isServer || context.Sender is null)
                {
                    break;
                }

                var interest = project.FindSubsystem<SubsystemNetworkInterest>(true)!;
                foreach (var e in message4.EntityIdList)
                {
                    project.FindEntityById(
                        e,
                        entity =>
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
                                        interest.Entities.AddObserved(
                                            context.Sender,
                                            EntityInterestGroup.BlockEntities,
                                            entity.EntityId
                                        );
                                    }

                                    return;
                                }

                                foreach (var member in SubsystemNetworkInterest.GetBodyGroup(body))
                                {
                                    el.Add(member.Entity);
                                    interest.Entities.AddObserved(
                                        context.Sender,
                                        EntityInterestGroup.Creatures,
                                        member.Entity.EntityId
                                    );
                                }
                            }
                        }
                    );
                }

                if (context.Sender is not null)
                {
                    netNode.QueuePackage(
                        EntityPackageBuilder.Capture(project, el.Distinct().ToList()),
                        PackageAudience.To(context.Sender)
                    );
                }

                break;
            }
        }
    }
}
