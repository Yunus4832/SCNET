using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Network.Snapshots;

public static class EntitySnapshotSerializer
{
    public static byte[] Serialize(Project project, IEnumerable<Entity> entities)
    {
        var values = new ValuesDictionary();
        project.SaveEntitiesAll(entities).Save(values);
        return values.ToMessagePack();
    }

    public static List<Entity> Deserialize(Project project, byte[] data)
    {
        var values = new ValuesDictionary();
        values.ApplyOverridesUseMessagePack(data);
        var entityData = new EntityDataList(project.GameDatabase, values, false);
        // Remove old components before replacements register the same inventories and entity IDs.
        foreach (var entity in entityData.EntitiesData)
        {
            project.FindEntityById(entity.Id, existing => project.RemoveEntity(existing, true));
        }

        return project.LoadEntitiesAll(entityData);
    }

    public static CharacterEntitySnapshot[] CaptureCharacterEntities(IEnumerable<Entity> entities) => entities
        .Select(entity => entity.FindComponent<ComponentCharacter>())
        .OfType<ComponentCharacter>()
        .Select(player => new CharacterEntitySnapshot(
                player.CharacterData.CharacterId,
                player.Entity.EntityId,
                player.ComponentBody.Position,
                player.ComponentBody.ParentBody?.Entity.EntityId ?? 0
            )
        )
        .ToArray();
}
