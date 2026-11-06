using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Network.Snapshots;

public static class EntityPackageBuilder
{
    public static bool ShouldSend(Entity entity) =>
        entity.FindComponent<ComponentCharacter>() is not { } player ||
        player.CharacterData.ControllerConnection != null;

    public static SingleEntitySnapshotPackage Capture(Entity entity) =>
        (SingleEntitySnapshotPackage)Capture(entity.Project, [entity], true);

    public static EntityPackage Capture(Project project, IEnumerable<Entity> source, bool single = false)
    {
        var entities = source as IReadOnlyList<Entity> ?? source.ToArray();
        var players = entities.Select(entity => entity.FindComponent<ComponentCharacter>()?.CharacterData)
            .OfType<CharacterData>().Distinct().Select(player =>
                {
                    var values = new ValuesDictionary();
                    player.Save(values);
                    return new CharacterSnapshot(
                        values.ToMessagePack(),
                        player.ControllerConnection is { } client
                            ? new CharacterControllerSnapshot(
                                client.ID,
                                client.CharacterId,
                                client.PlayerAddress,
                                client.State,
                                client.ConnectionEpoch,
                                client.ConnectionGeneration
                            )
                            : null
                    );
                }
            ).ToArray();
        return ((single
            ? new SingleEntitySnapshotPackage
            {
                Characters = players, EntityData = EntitySnapshotSerializer.Serialize(project, entities),
                CharacterEntities = EntitySnapshotSerializer.CaptureCharacterEntities(entities)
            }
            : new EntitySnapshotPackage
            {
                Characters = players, EntityData = EntitySnapshotSerializer.Serialize(project, entities),
                CharacterEntities = EntitySnapshotSerializer.CaptureCharacterEntities(entities)
            }));
    }
}
