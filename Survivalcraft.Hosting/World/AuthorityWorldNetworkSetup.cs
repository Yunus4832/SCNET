using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network.Replication;

namespace Game.World;

public static class AuthorityWorldNetworkSetup
{
    public static void Attach(Project project, NetNode net, BlockTextureStore textures)
    {
        var entityMaps = BuildServerEntityMap(project);
        project.BeforeEntityAdded += (_, arg) => EnsureEntityId(arg.Entity, entityMaps);
        project.EntityRemoved += (_, arg) => { entityMaps.Remove(arg.Entity.EntityId); };
        project.EntityAdded += (_, arg) =>
        {
            if (!EntityPackageBuilder.ShouldSend(arg.Entity) ||
                IsInterestManagedBody(arg.Entity) ||
                arg.Entity.FindComponent<ComponentBlockEntity>() is not null)
            {
                return;
            }

            net.QueuePackage(EntityPackageBuilder.Capture(arg.Entity), PackageAudience.Global);
        };
        project.EntityRemoved += (_, arg) =>
        {
            if (arg.Entity.FindComponent<ComponentBlockEntity>() is not null)
            {
                var interest = project.FindSubsystem<SubsystemNetworkInterest>(true)!;
                net.QueuePackage(
                    new EntityRemovedPackage { EntityId = arg.Entity.EntityId },
                    PackageAudience.To(
                        interest.Entities.GetObservers(EntityInterestGroup.BlockEntities, arg.Entity.EntityId).ToArray()
                    )
                );
                interest.Entities.RemoveEntity(EntityInterestGroup.BlockEntities, arg.Entity.EntityId);
                return;
            }

            if (!IsInterestManagedBody(arg.Entity))
            {
                net.QueuePackage(
                    new EntityRemovedPackage { EntityId = arg.Entity.EntityId },
                    PackageAudience.Global
                );
            }
        };
        net.OnClientStateChanged += client => OnServerClientStateChanged(project, net, client);
        net.OnClientTransportConnected += client => SendBootstrap(project, net, client, textures);
        net.OnClientBootstrapApplied += client => SendInitialWorldSnapshot(project, net, client);
        net.OnClientBecameLive += client => CompleteClientJoin(project, client);
    }

    private static Dictionary<int, Entity> BuildServerEntityMap(Project project)
    {
        var entityMaps = new Dictionary<int, Entity>();
        foreach (var entity in project.EntityKeys.Where(entity => entity.EntityId != 0))
        {
            EnsureEntityId(entity, entityMaps);
        }

        return entityMaps;
    }

    private static void EnsureEntityId(Entity entity, Dictionary<int, Entity> entityMaps)
    {
        if (entity.EntityId != 0 && entityMaps.TryAdd(entity.EntityId, entity))
        {
            return;
        }

        for (ushort i = 1; i < ushort.MaxValue; i++)
        {
            if (entityMaps.ContainsKey(i))
            {
                continue;
            }

            entity.EntityId = i;
            entityMaps.Add(i, entity);
            return;
        }
    }

    private static bool IsInterestManagedBody(Entity entity)
    {
        var body = entity.FindComponent<ComponentBody>();
        return body is not null;
    }

    private static void OnServerClientStateChanged(Project project, NetNode net, WorldConnection client)
    {
        net.QueuePackage(
            ClientPackageBuilder.State(client),
            PackageAudience.Except(client)
        );
        switch (client.State)
        {
            case ClientState.NotConnected:
                project.FindSubsystem<SubsystemNetworkInterest>(true)!.Entities.RemoveClient(client);
                var subsystemCharacters = project.FindSubsystem<SubsystemCharacters>(true)!;
                subsystemCharacters.MakeCharacterOffline(client.CharacterId);
                net.QueuePackage(CharacterPackageBuilder.Capture(subsystemCharacters), PackageAudience.Global);
                GC.Collect();
                break;
            case ClientState.LoadTerrain:
                GC.Collect();
                if (WorldConnectionResolver.FindCharacter(client)?.ComponentCharacter is
                    { } spawningCharacter)
                {
                    if (project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.GameMode != GameMode.Creative)
                    {
                        spawningCharacter.ComponentHealth.IsInvulnerable = true;
                    }
                }

                break;
            case ClientState.Playing:
                if (WorldConnectionResolver.FindCharacter(client)?.ComponentCharacter is
                    { } playingCharacter)
                {
                    if (project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.GameMode != GameMode.Creative)
                    {
                        playingCharacter.ComponentHealth.IsInvulnerable = false;
                    }
                }

                break;
        }
    }

    private static void SendBootstrap(Project project, NetNode net, Client client, BlockTextureStore textures)
    {
        var subsystemCharacters = project.FindSubsystem<SubsystemCharacters>(true)!;
        try
        {
            if (WorldConnectionResolver.FindPlayer(client) is not { } subject || subject.Ban is not null)
            {
                net.RemoveClient(client, "用户未获准进入此存档");
                return;
            }

            var restored = subsystemCharacters.MakeCharacterOnline(
                client.CharacterId,
                out var characterData,
                out var entity
            );
            if (subject.CharacterId != Guid.Empty && !restored)
            {
                throw new InvalidDataException("The subject's persisted character data is missing.");
            }

            if (restored)
            {
                client.CachePlayerEntity = entity;
                net.QueuePackage(CharacterPackageBuilder.Capture(subsystemCharacters), PackageAudience.Global);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"玩家数据恢复失败，断开连接: guid={client.CharacterId:N}, error={ex.Message}");
            net.RemoveClient(client, "玩家数据加载失败，请稍后重试");
            return;
        }

        byte[]? textureData = null;
        var subsystemGameInfo = project.FindSubsystem<SubsystemGameInfo>(true)!;
        if (!string.IsNullOrEmpty(subsystemGameInfo.WorldSettings.BlocksTextureName))
        {
            using var s = Storage.OpenFile(
                textures.GetFileName(subsystemGameInfo.WorldSettings.BlocksTextureName),
                OpenFileMode.Read
            );
            textureData = StreamUtils.ReadBytes(s);
        }

        var data = CaptureBootstrapData(project);
        if (data == null)
        {
            Log.Error($"Failed to get project data for client {client.ID}, disconnecting...");
            client.Peer?.Disconnect();
            return;
        }

        client.ConnectionPhase = ConnectionPhase.BootstrapSent;
        net.QueuePackage(
            WorldSnapshotBuilder.Bootstrap(
                project,
                client.ConnectionEpoch,
                net.Clients.Values,
                textureData,
                data,
                WorldContentBinding.For(project).Catalog.Registry.Mapping
            ),
            PackageAudience.To(client)
        );
    }

    private static void SendInitialWorldSnapshot(Project project, NetNode net, Client client)
    {
        var subsystemCharacters = project.FindSubsystem<SubsystemCharacters>(true)!;
        var subsystemNetworkInterest = project.FindSubsystem<SubsystemNetworkInterest>(true)!;
        var sendList = project.EntityKeys
            .Where(EntityPackageBuilder.ShouldSend)
            .Where(entity => subsystemNetworkInterest.ShouldIncludeInInitialSnapshot(client, entity))
            .ToList();
        subsystemNetworkInterest.SeedInitialSnapshot(client, sendList);
        client.ConnectionPhase = ConnectionPhase.WorldSnapshotSent;
        net.QueuePackage(
            WorldSnapshotBuilder.Initial(
                client.ConnectionEpoch,
                project,
                net.Clients.Values,
                subsystemCharacters.Characters,
                sendList
            ),
            PackageAudience.To(client)
        );
    }

    private static void CompleteClientJoin(Project project, Client client)
    {
        if (client.CachePlayerEntity == null)
        {
            return;
        }

        project.AddEntity(client.CachePlayerEntity);
        client.CachePlayerEntity = null;
        // A restored entity may already be attached from world loading. AddEntity then emits
        // no event, so refresh the connection-dependent player list after the join completes.
        project.FindSubsystem<SubsystemCharacters>(true)!.UpdateComponentCharacters();
    }

    private static byte[] CaptureBootstrapData(Project project)
    {
        var previousMode = project.SendToClientMode;
        project.SendToClientMode = true;
        try
        {
            var root = new ValuesDictionary();
            var data = project.Save();
            data.EntityDataList.EntitiesData.Clear();
            data.Save(root);
            return root.ToMessagePack();
        }
        finally { project.SendToClientMode = previousMode; }
    }
}
