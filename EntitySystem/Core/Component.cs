using Engine.Core;

using EntitySystem.TemplatesDatabase;

namespace EntitySystem.Core;

public abstract class Component : IDisposable
{

    public ValuesDictionary ValuesDictionary
    {
        get => field is not null ? field : throw new InvalidOperationException("Component was not initialized");
        private set;
    } = null!;


    public Entity Entity
    {
        get =>  field is not null ? field : throw new InvalidOperationException("Component was not initialized");
        private set;
    } = null!;

    public Project Project => Entity.Project;

    public bool IsAddedToProject => Entity.IsAddedToProject;

    public virtual void Dispose()
    {
    }

    public virtual IEnumerable<Entity> GetOwnedEntities()
    {
        return ReadOnlyList<Entity>.Empty;
    }

    public virtual void OnEntityAdded()
    {
    }

    public virtual void OnEntityRemoved()
    {
    }

    public virtual void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
    }

    public virtual void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
    }

    public void Initialize(Entity entity, ValuesDictionary valuesDictionary)
    {
        if (valuesDictionary.DatabaseObject.Type != entity.Project.GameDatabase.MemberComponentTemplateType)
        {
            throw new InvalidOperationException("ValuesDictionary has invalid type.");
        }

        Entity = entity;
        ValuesDictionary = valuesDictionary;
    }
}
