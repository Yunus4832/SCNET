using Engine.Core;
using Engine.Serialization;
using EntitySystem.TemplatesDatabase;

namespace EntitySystem.Core;

public class Project : IDisposable
{
    protected readonly Dictionary<Entity, bool> entityDictionary = new();

    protected readonly List<Subsystem> subsystems = [];

    public GameDatabase GameDatabase { get; protected init; }

    public DatabaseObject ProjectTemplate { get; protected init; }

    public ReadOnlyList<Subsystem> ReadOnlySubsystems => new(subsystems);

    public Dictionary<Entity, bool>.KeyCollection EntityKeys => entityDictionary.Keys;

    public event EventHandler<EntityAddRemoveEventArgs>? BeforeEntityAdded;

    public event EventHandler<EntityAddRemoveEventArgs>? EntityAdded;

    public event EventHandler<EntityAddRemoveEventArgs>? EntityRemoved;

    public static event Action<Project>? OnProjectLoad;

    public Project()
    {
        GameDatabase = null!;
        ProjectTemplate = null!;
    }

    public Project(GameDatabase gameDatabase, ProjectData projectData)
    {
        try
        {
            GameDatabase = gameDatabase;
            ProjectTemplate = projectData.ValuesDictionary.DatabaseObject;
            var dictionary = new Dictionary<string, Subsystem>();
            foreach (var item in from x in projectData.ValuesDictionary.Values
                     select x as ValuesDictionary
                     into x
                     where x?.DatabaseObject != null &&
                           x.DatabaseObject.Type == gameDatabase.MemberSubsystemTemplateType
                     select x)
            {
                var value = item.GetValue<bool>("IsOptional");
                var value2 = item.GetValue<string>("Class");
                var type = TypeCache.FindType(value2, false, !value);
                if (type is null)
                {
                    continue;
                }

                object? obj;
                try
                {
                    obj = Activator.CreateInstance(type);
                }
                catch (TargetInvocationException ex)
                {
                    throw ex.InnerException ?? ex;
                }

                if (obj is not Subsystem subsystem)
                {
                    throw new InvalidOperationException(
                        $"Type \"{value2}\" cannot be used as a subsystem because it does not inherit from Subsystem class.");
                }

                subsystem.Initialize(this, item);
                dictionary.Add(item.DatabaseObject.Name, subsystem);
                subsystems.Add(subsystem);
            }

            var loadedSubsystems = new Dictionary<Subsystem, bool>();
            foreach (var value3 in dictionary.Values)
            {
                LoadSubsystem(value3, dictionary, loadedSubsystems, 0);
            }

            OnProjectLoad?.Invoke(this);
            var entities = LoadEntities(projectData.EntityDataList);
            AddEntities(entities);
        }
        catch (Exception)
        {
            try
            {
                Dispose();
            }
            catch (Exception)
            {
                // ignored
            }

            throw;
        }
    }


    public void Dispose()
    {
        foreach (var key in entityDictionary.Keys)
        {
            key.Dispose();
        }

        foreach (var subsystem in subsystems)
        {
            subsystem.Dispose();
        }

        OnProjectLoad = null;
        GC.SuppressFinalize(this);
    }

    public Subsystem? FindSubsystem(Type type, string? name, bool throwOnError)
    {
        foreach (var subsystem in subsystems)
        {
            var isAssignable = type.GetTypeInfo().IsAssignableFrom(subsystem.GetType().GetTypeInfo());
            var nameMatch = string.IsNullOrEmpty(name) || subsystem.ValuesDictionary.DatabaseObject.Name == name;
            if (isAssignable && nameMatch)
            {
                return subsystem;
            }
        }

        if (!throwOnError)
        {
            return null;
        }

        if (name is not null)
        {
            throw new Exception(
                $"Required subsystem {type.FullName} with name \"{name}\" does not exist in project.");
        }

        throw new Exception($"Required subsystem {type.FullName} does not exist in project.");
    }

    public T? FindSubsystem<T>() where T : class
    {
        return FindSubsystem(typeof(T), null, false) as T;
    }

    public T? FindSubsystem<T>(bool throwOnError) where T : class
    {
        return FindSubsystem(typeof(T), null, throwOnError) as T;
    }

    public virtual bool FindEntityById(ushort id, Action<Entity>? action = null)
    {
        return false;
    }

    public T? FindSubsystem<T>(string name, bool throwOnError) where T : class
    {
        return FindSubsystem(typeof(T), name, throwOnError) as T;
    }

    public IEnumerable<Subsystem> FindSubsystems(Type type)
    {
        return subsystems.Where(subsystem => type.GetTypeInfo().IsAssignableFrom(subsystem.GetType().GetTypeInfo()));
    }

    public IEnumerable<T> FindSubsystems<T>() where T : class
    {
        return subsystems.OfType<T>();
    }

    public virtual void GenerateEntityId(Entity entity)
    {
    }

    public Entity CreateEntity(ValuesDictionary valuesDictionary)
    {
        try
        {
            var entity = new Entity(this, valuesDictionary);
            var idToEntityMap = new IdToEntityMap(new Dictionary<int, Entity>());
            entity.PublicLoadEntity(valuesDictionary, idToEntityMap);
            return entity;
        }
        catch (Exception innerException)
        {
            throw new Exception($"Error creating entity from template \"{valuesDictionary.DatabaseObject.Name}\".",
                innerException);
        }
    }

    public virtual void AddEntity(Entity entity)
    {
        if (entity.Project != this)
        {
            throw new Exception("Entity does not belong to this project.");
        }

        if (entity.IsAddedToProject)
        {
            return;
        }

        entityDictionary.Add(entity, true);
        entity.IsAddedToProject = true;
        FireEntityAddedEvents(entity);
    }

    public virtual void RemoveEntity(Entity entity, bool disposeEntity)
    {
        if (entity.Project != this)
        {
            throw new Exception("Entity does not belong to this project.");
        }

        if (!entity.IsAddedToProject)
        {
            return;
        }

        entityDictionary.Remove(entity);
        entity.IsAddedToProject = false;
        FireEntityRemovedEvents(entity);
        if (disposeEntity)
        {
            entity.Dispose();
        }
    }

    public void AddEntities(IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
        {
            AddEntity(entity);
        }
    }

    public void RemoveEntities(IEnumerable<Entity> entities, bool disposeEntities)
    {
        foreach (var entity in entities)
        {
            RemoveEntity(entity, disposeEntities);
        }
    }

    public virtual List<Entity> LoadEntities(EntityDataList entityDataList)
    {
        var list = new List<Entity>(entityDataList.EntitiesData.Count);
        var dictionary = new Dictionary<int, Entity>();
        var idToEntityMap = new IdToEntityMap(dictionary);
        foreach (var entitiesDatum in entityDataList.EntitiesData)
        {
            try
            {
                var entity = new Entity(this, entitiesDatum.ValuesDictionary);
                list.Add(entity);
                if (entitiesDatum.Id != 0)
                {
                    dictionary.Add(entitiesDatum.Id, entity);
                }
            }
            catch (Exception innerException)
            {
                throw new Exception(
                    $"Error creating entity from template \"{entitiesDatum.ValuesDictionary.DatabaseObject.Name}\".",
                    innerException);
            }
        }

        var num = 0;
        var entitiesToRemove = new List<Entity>();
        foreach (var entitiesDatum2 in entityDataList.EntitiesData)
        {
            try
            {
                list[num].PublicLoadEntity(entitiesDatum2.ValuesDictionary, idToEntityMap);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load entity {entitiesDatum2.Id}, will skip it. Error: {ex.Message}");
                entitiesToRemove.Add(list[num]);
            }
            num++;
        }

        foreach (var entity in entitiesToRemove)
        {
            list.Remove(entity);
        }

        return list;
    }

    public virtual EntityDataList SaveEntities(IEnumerable<Entity> entities)
    {
        var dictionary = DetermineNotOwnedEntities(entities);
        var dictionary2 = new Dictionary<Entity, int>();
        var entityToIdMap = new EntityToIdMap(dictionary2);
        foreach (var key in dictionary.Keys)
        {
            dictionary2.Add(key, key.EntityId);
        }

        var entityDataList = new EntityDataList
        {
            EntitiesData = new List<EntityData>(dictionary.Keys.Count)
        };
        foreach (var key2 in dictionary.Keys)
        {
            var entityData = new EntityData
            {
                Id = entityToIdMap.FindId(key2),
                ValuesDictionary = new ValuesDictionary
                {
                    DatabaseObject = key2.ValuesDictionary.DatabaseObject
                }
            };
            key2.InternalSaveEntity(entityData.ValuesDictionary, entityToIdMap);
            entityDataList.EntitiesData.Add(entityData);
        }

        return entityDataList;
    }

    public ProjectData Save()
    {
        var projectData = new ProjectData
        {
            ValuesDictionary = new ValuesDictionary
            {
                DatabaseObject = ProjectTemplate
            }
        };
        foreach (var subsystem in ReadOnlySubsystems)
        {
            var valuesDictionary = new ValuesDictionary();
            subsystem.Save(valuesDictionary);
            if (valuesDictionary.Count > 0)
            {
                projectData.ValuesDictionary.SetValue(subsystem.ValuesDictionary.DatabaseObject.Name, valuesDictionary);
            }
        }

        projectData.EntityDataList = SaveEntities(EntityKeys);
        return projectData;
    }

    public void FireEntityAddedEvents(Entity entity)
    {
        BeforeEntityAdded?.Invoke(this, new EntityAddRemoveEventArgs(entity));
        foreach (var component in entity.Components)
        {
            component.OnEntityAdded();
        }

        foreach (var subsystem in ReadOnlySubsystems)
        {
            subsystem.OnEntityAdded(entity);
        }

        EntityAdded?.Invoke(this, new EntityAddRemoveEventArgs(entity));
        entity.FireEntityAddedEvent();
    }

    public void FireEntityRemovedEvents(Entity entity)
    {
        foreach (var component in entity.Components)
        {
            component.OnEntityRemoved();
        }

        foreach (var subsystem in ReadOnlySubsystems)
        {
            subsystem.OnEntityRemoved(entity);
        }

        EntityRemoved?.Invoke(this, new EntityAddRemoveEventArgs(entity));
        entity.FireEntityRemovedEvent();
    }

    public static Dictionary<Entity, bool> DetermineNotOwnedEntities(IEnumerable<Entity> entities)
    {
        var dictionary = new Dictionary<Entity, bool>();
        var list = new List<Entity>();
        foreach (var entity in entities)
        {
            dictionary.Add(entity, true);
            var list2 = entity.InternalGetOwnedEntities();
            list.AddRange(list2);
        }

        for (var i = 0; i < list.Count; i++)
        {
            var list3 = list[i].InternalGetOwnedEntities();
            list.AddRange(list3);
            dictionary.Remove(list[i]);
        }

        return dictionary;
    }

    public void LoadSubsystem(Subsystem subsystem, Dictionary<string, Subsystem> subsystemsByName,
        Dictionary<Subsystem, bool> loadedSubsystems, int depth)
    {
        var realTime1 = Time.RealTime;
        if (depth > 100)
        {
            throw new InvalidOperationException(
                $"Too deep dependencies recursion while loading subsystem \"{subsystem.ValuesDictionary.DatabaseObject.Name}\".");
        }

        if (loadedSubsystems.ContainsKey(subsystem))
        {
            return;
        }

        var value = subsystem.ValuesDictionary.GetValue("Dependencies", string.Empty);
        if (!string.IsNullOrEmpty(value))
        {
            var array = value.Split([','], StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in array)
            {
                var text = item.Trim();
                if (subsystemsByName.TryGetValue(text, out var value2))
                {
                    LoadSubsystem(value2, subsystemsByName, loadedSubsystems, depth + 1);
                    continue;
                }

                throw new InvalidOperationException(
                    $"Dependency subsystem \"{text}\" not found when loading subsystem \"{subsystem.ValuesDictionary.DatabaseObject.Name}\".");
            }
        }

        subsystem.Load(subsystem.ValuesDictionary);
        loadedSubsystems.Add(subsystem, true);
    }
}
