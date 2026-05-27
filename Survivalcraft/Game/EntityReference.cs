using System.Globalization;

using EntitySystem.Core;

namespace Game;

public struct EntityReference
{
    public enum ReferenceType
    {
        Null,
        Local,
        ByEntityId,
        ByEntityName
    }

    private ReferenceType _referenceType;

    private string _entityReference;

    private string _componentReference;

    public string ReferenceString
    {
        get
        {
            return _referenceType switch
            {
                ReferenceType.Null => "null:",
                ReferenceType.Local => $"local:{_componentReference}",
                ReferenceType.ByEntityId => $"id:{_entityReference}:{_componentReference}",
                ReferenceType.ByEntityName => $"name:{_entityReference}:{_componentReference}",
                _ => throw new Exception("Unknown entity reference type.")
            };
        }
    }

    public static EntityReference Null => default;

    public Entity? GetEntity(Entity localEntity, IdToEntityMap idToEntityMap, bool throwIfNotFound)
    {
        Entity? entity;
        if (_referenceType == ReferenceType.Null)
        {
            entity = null;
        }
        else if (_referenceType == ReferenceType.Local)
        {
            entity = localEntity;
        }
        else if (_referenceType == ReferenceType.ByEntityId)
        {
            var id = int.Parse(_entityReference, CultureInfo.InvariantCulture);
            entity = idToEntityMap.FindEntity(id);
        }
        else
        {
            if (_referenceType != ReferenceType.ByEntityName)
            {
                throw new Exception("Unknown entity reference type.");
            }

            entity = localEntity.Project.FindSubsystem<SubsystemNames>(true)!.FindEntityByName(_entityReference);
        }

        if (entity != null)
        {
            return entity;
        }

        return throwIfNotFound ? throw new Exception($"Required entity \"{ReferenceString}\" not found.") : null;
    }

    public T? GetComponent<T>(Entity localEntity, IdToEntityMap idToEntityMap, bool throwIfNotFound) where T : class
    {
        var entity = GetEntity(localEntity, idToEntityMap, throwIfNotFound);
        return entity?.FindComponent<T>(_componentReference, throwIfNotFound);
    }

    public bool IsNullOrEmpty()
    {
        return _referenceType == 0 ||
               (_referenceType == ReferenceType.Local && string.IsNullOrEmpty(_componentReference)) ||
               (_referenceType == ReferenceType.ByEntityId && _entityReference == "0") ||
               _referenceType == ReferenceType.ByEntityName && string.IsNullOrEmpty(_entityReference);
    }

    public static EntityReference Local(Component? component)
    {
        EntityReference result = default;
        result._referenceType = ReferenceType.Local;
        result._componentReference = component != null ? component.ValuesDictionary.DatabaseObject.Name : string.Empty;
        return result;
    }

    public static EntityReference FromId(Component? component, EntityToIdMap entityToIdMap)
    {
        var num = entityToIdMap.FindId(component?.Entity);
        EntityReference result = default;
        result._referenceType = ReferenceType.ByEntityId;
        result._entityReference = num.ToString(CultureInfo.InvariantCulture);
        result._componentReference = component != null ? component.ValuesDictionary.DatabaseObject.Name : string.Empty;
        return result;
    }

    public static EntityReference FromId(Entity entity, EntityToIdMap entityToIdMap)
    {
        var num = entityToIdMap.FindId(entity);
        EntityReference result = default;
        result._referenceType = ReferenceType.ByEntityId;
        result._entityReference = num.ToString(CultureInfo.InvariantCulture);
        result._componentReference = string.Empty;
        return result;
    }

    public static EntityReference FromName(Component? component)
    {
        var entityReference = component != null
            ? component.Entity.FindComponent<ComponentName>(string.Empty, true)!.Name
            : string.Empty;
        EntityReference result = default;
        result._referenceType = ReferenceType.ByEntityName;
        result._entityReference = entityReference;
        result._componentReference = component != null ? component.ValuesDictionary.DatabaseObject.Name : string.Empty;
        return result;
    }

    public static EntityReference FromName(Entity? entity)
    {
        var entityReference = entity != null
            ? entity.FindComponent<ComponentName>(string.Empty, true)!.Name
            : string.Empty;
        EntityReference result = default;
        result._referenceType = ReferenceType.ByEntityName;
        result._entityReference = entityReference;
        result._componentReference = string.Empty;
        return result;
    }

    public static EntityReference FromReferenceString(string referenceString)
    {
        EntityReference result = default;
        if (string.IsNullOrEmpty(referenceString))
        {
            result._referenceType = ReferenceType.Null;
            result._entityReference = string.Empty;
            result._componentReference = string.Empty;
        }
        else
        {
            var array = referenceString.Split(':');
            if (array.Length == 1)
            {
                result._referenceType = ReferenceType.Local;
                result._entityReference = string.Empty;
                result._componentReference = array[0];
            }
            else
            {
                if (array.Length != 2 && array.Length != 3)
                {
                    throw new Exception("Invalid entity reference. Too many tokens.");
                }

                if (array[0] == "null" && array.Length == 2)
                {
                    result._referenceType = ReferenceType.Null;
                    result._entityReference = string.Empty;
                    result._componentReference = string.Empty;
                }
                else if (array[0] == "local" && array.Length == 2)
                {
                    result._referenceType = ReferenceType.Local;
                    result._componentReference = array[1];
                }
                else if (array[0] == "id")
                {
                    result._referenceType = ReferenceType.ByEntityId;
                    result._entityReference = array[1];
                    result._componentReference = array.Length == 3 ? array[2] : string.Empty;
                }
                else
                {
                    if (array[0] != "name")
                    {
                        throw new Exception("Unknown entity reference type.");
                    }

                    result._referenceType = ReferenceType.ByEntityId;
                    result._entityReference = array[1];
                    result._componentReference = array.Length == 3 ? array[2] : string.Empty;
                }
            }
        }

        return result;
    }
}
