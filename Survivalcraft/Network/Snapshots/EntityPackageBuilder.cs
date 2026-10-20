using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Network.Snapshots;

public static class EntityPackageBuilder
{
    public static bool ShouldSend(Entity entity) => RunMode.Value == RunModeType.Gui ||
        entity.FindComponent<ComponentPlayer>() is not { } player || player.PlayerData.Client != null;

    public static EntityPackage Capture(Entity entity) => Capture(entity.Project, [entity], true);

    public static EntityPackage Capture(Project project, IEnumerable<Entity> source, bool single = false)
    {
        var entities = source as IReadOnlyList<Entity> ?? source.ToArray();
        var players = entities.Select(entity => entity.FindComponent<ComponentPlayer>()?.PlayerData)
            .OfType<PlayerData>().Distinct().Select(player =>
            {
                var values = new ValuesDictionary();
                player.Save(values);
                return new PlayerSnapshot(values.ToMessagePack(), player.Client is { } client ? ClientPackageBuilder.Capture(client) : null);
            }).ToArray();
        return new EntityPackage
        {
            Type = single ? EntityPackage.EventType.LoadOne : EntityPackage.EventType.LoadList,
            Players = players,
            EntityData = EntitySnapshotSerializer.Serialize(project, entities),
            PlayerEntities = EntitySnapshotSerializer.CapturePlayerEntities(entities)
        };
    }
}
