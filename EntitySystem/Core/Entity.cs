using System.Collections;
using Engine.Core;
using Engine.Serialization;
using EntitySystem.TemplatesDatabase;

namespace EntitySystem.Core;

public class Entity : IDisposable
{
    private readonly List<Component> _components;

    public ushort EntityId { get; set; }

    public Project Project { get; }

    public ValuesDictionary ValuesDictionary { get; }

    public bool IsAddedToProject { get; set; }

    public ReadOnlyList<Component> Components => new(_components);

    public event EventHandler? EntityAdded;

    public event EventHandler? EntityRemoved;


    public Entity(Project project, ValuesDictionary valuesDictionary)
    {
        if (valuesDictionary.DatabaseObject.Type != project.GameDatabase.EntityTemplateType)
        {
            throw new InvalidOperationException("ValuesDictionary was not created from EntityTemplate.");
        }

        Project = project;
        ValuesDictionary = valuesDictionary;
        var list = new List<KeyValuePair<int, Component>>();
        var items = from x in valuesDictionary.Values
            select x as ValuesDictionary
            into x
            where x is { DatabaseObject: not null } &&
                  x.DatabaseObject.Type == project.GameDatabase.MemberComponentTemplateType
            select x;

        foreach (var item in items)
        {
            var value = item.GetValue<bool>("IsOptional");
            var value2 = item.GetValue<string>("Class");
            var value3 = item.GetValue<int>("LoadOrder");
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

            if (obj is not Component component)
            {
                throw new InvalidOperationException(
                    $"Type \"{value2}\" cannot be used as a component because it does not inherit from Component class.");
            }

            component.Initialize(this, item);
            list.Add(new KeyValuePair<int, Component>(value3, component));
        }

        list.Sort((x, y) => x.Key - y.Key);
        _components = new List<Component>(list.Select(x => x.Value));
    }

    public void Dispose()
    {
        foreach (var component in _components)
        {
            try
            {
                component.Dispose();
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }

    public Component? FindComponent(Type type, string name, bool throwOnError)
    {
        foreach (var component in _components)
        {
            var typeAssignable = type.GetTypeInfo().IsAssignableFrom(component.GetType().GetTypeInfo());
            var nameMatch = string.IsNullOrEmpty(name) || component.ValuesDictionary.DatabaseObject.Name == name;
            if (typeAssignable && nameMatch)
            {
                return component;
            }
        }

        if (!throwOnError)
        {
            return null;
        }

        if (string.IsNullOrEmpty(name))
        {
            throw new Exception($"Required component {type.FullName} does not exist in entity.");
        }

        throw new Exception($"Required component {type.FullName} with name \"{name}\" does not exist in entity.");
    }

    public T? FindComponent<T>() where T : class
    {
        return FindComponent(typeof(T), string.Empty, false) as T;
    }

    public T? FindComponent<T>(bool throwOnError) where T : class
    {
        return FindComponent(typeof(T), string.Empty, throwOnError) as T;
    }

    public T? FindComponent<T>(string name, bool throwOnError) where T : class
    {
        return FindComponent(typeof(T), name, throwOnError) as T;
    }

    public FilteredComponentsEnumerable<T> FindComponents<T>() where T : class
    {
        return new FilteredComponentsEnumerable<T>(this);
    }

    public List<Entity> InternalGetOwnedEntities()
    {
        var list = new List<Entity>();
        foreach (var ownedEntities in _components.Select(component => component.GetOwnedEntities()))
        {
            list.AddRange(ownedEntities);
        }

        return list;
    }

    public void PublicLoadEntity(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        foreach (var component in _components)
        {
            try
            {
                component.Load(component.ValuesDictionary, idToEntityMap);
            }
            catch (Exception innerException)
            {
                throw new InvalidOperationException(
                    $"Error loading component {component.GetType().FullName}.",
                    innerException
                );
            }
        }
    }

    public void InternalSaveEntity(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        foreach (var component in _components)
        {
            var valuesDictionary2 = new ValuesDictionary();
            component.Save(valuesDictionary2, entityToIdMap);
            if (valuesDictionary2.Count > 0)
            {
                valuesDictionary.SetValue(component.ValuesDictionary.DatabaseObject.Name, valuesDictionary2);
            }
        }
    }

    public void FireEntityAddedEvent()
    {
        EntityAdded?.Invoke(this, EventArgs.Empty);
    }

    public void FireEntityRemovedEvent()
    {
        EntityRemoved?.Invoke(this, EventArgs.Empty);
    }

    public readonly struct FilteredComponentsEnumerable<T>(Entity entity) : IEnumerable<T>
        where T : class
    {
        public FilteredComponentsEnumerator<T> GetEnumerator()
        {
            return new FilteredComponentsEnumerator<T>(entity);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new FilteredComponentsEnumerator<T>(entity);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new FilteredComponentsEnumerator<T>(entity);
        }
    }

    public struct FilteredComponentsEnumerator<T>(Entity entity) : IEnumerator<T?>
        where T : class
    {
        private int _index = 0;

        public T? Current { get; private set; } = null;

        object? IEnumerator.Current => Current;

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            while (_index < entity._components.Count)
            {
                if (entity._components[_index++] is not T val)
                {
                    continue;
                }

                Current = val;
                return true;
            }

            Current = null;
            return false;
        }

        public void Reset()
        {
            _index = 0;
            Current = null;
        }
    }
}
