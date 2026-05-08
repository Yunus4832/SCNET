using Engine.Core;

namespace EntitySystem.TemplatesDatabase;

public class Database
{
    private readonly Dictionary<Guid, DatabaseObject> _databaseObjectsByGuid = new();

    private readonly ReadOnlyList<DatabaseObjectType> _databaseObjectTypes;

    public Database(DatabaseObject root, IEnumerable<DatabaseObjectType> databaseObjectTypes)
    {
        var objectTypes = databaseObjectTypes as DatabaseObjectType[] ?? databaseObjectTypes.ToArray();
        if (!objectTypes.Contains(root.Type))
        {
            throw new InvalidOperationException("Database root has invalid database object type.");
        }

        if (root.NestingParent != null)
        {
            throw new InvalidOperationException("Database root cannot be nested.");
        }

        _databaseObjectTypes = new ReadOnlyList<DatabaseObjectType>(new List<DatabaseObjectType>(objectTypes));
        Root = root;
        Root.Database = this;
    }

    public IList<DatabaseObjectType> DatabaseObjectTypes => _databaseObjectTypes;

    public DatabaseObject Root { get; }

    public DatabaseObjectType? FindDatabaseObjectType(string name, bool throwIfNotFound)
    {
        var result = _databaseObjectTypes.FirstOrDefault(item => item.Name == name);
        if (result is not null)
        {
            return result;
        }

        return throwIfNotFound ? throw new Exception($"Required database object type \"{name}\" not found.") : null;
    }

    public DatabaseObject? FindDatabaseObject(Guid guid, DatabaseObjectType? type, bool throwIfNotFound)
    {
        if (!_databaseObjectsByGuid.TryGetValue(guid, out var value))
        {
            return throwIfNotFound
                ? throw new InvalidOperationException($"Required database object {guid} not found.")
                : null;
        }

        if (type != null && value.Type != type)
        {
            throw new InvalidOperationException(
                $"Database object {guid} has invalid type. Expected {type.Name}, found {value.Type.Name}.");
        }

        return value;
    }

    public DatabaseObject? FindDatabaseObject(string name, DatabaseObjectType type, bool throwIfNotFound)
    {
        return Root.FindExplicitNestedChild(name, type, false, throwIfNotFound);
    }

    public void FindUsedValueTypes(List<Type> typesList)
    {
        foreach (var explicitNestingChild in Root.GetExplicitNestingChildren(null, false))
        {
            if (explicitNestingChild.Value != null && !typesList.Contains(explicitNestingChild.Value.GetType()))
            {
                typesList.Add(explicitNestingChild.Value.GetType());
            }
        }
    }

    internal void AddDatabaseObject(DatabaseObject databaseObject, bool checkThatGuidsAreUnique)
    {
        if (databaseObject.Database != null)
        {
            throw new InvalidOperationException("Internal error: database object is already in a database.");
        }

        if (!_databaseObjectTypes.Contains(databaseObject.Type))
        {
            throw new InvalidOperationException(
                $"Database object type \"{databaseObject.Type.Name}\" is not supported by the database.");
        }

        if (checkThatGuidsAreUnique)
        {
            if (databaseObject.Guid != Guid.Empty && _databaseObjectsByGuid.ContainsKey(databaseObject.Guid))
            {
                throw new InvalidOperationException(
                    $"Database object {databaseObject.Guid} is already present in the database.");
            }

            foreach (var explicitNestingChild in databaseObject.GetExplicitNestingChildren(null, false))
            {
                if (explicitNestingChild.Guid != Guid.Empty &&
                    _databaseObjectsByGuid.ContainsKey(explicitNestingChild.Guid))
                {
                    throw new InvalidOperationException(
                        $"Database object {explicitNestingChild.Guid} is already present in the database.");
                }
            }
        }

        databaseObject.Database = this;
        if (databaseObject.Guid != Guid.Empty)
        {
            _databaseObjectsByGuid.Add(databaseObject.Guid, databaseObject);
        }

        foreach (var explicitNestingChild2 in databaseObject.GetExplicitNestingChildren(null, true))
        {
            AddDatabaseObject(explicitNestingChild2, false);
        }
    }

    internal void RemoveDatabaseObject(DatabaseObject databaseObject)
    {
        if (databaseObject.Database != this)
        {
            throw new InvalidOperationException("Internal error: database object is not in the database.");
        }

        databaseObject.Database = null!;
        if (databaseObject.Guid != Guid.Empty && !_databaseObjectsByGuid.Remove(databaseObject.Guid))
        {
            throw new InvalidOperationException("Internal error: database object not in dictionary.");
        }

        foreach (var explicitNestingChild in databaseObject.GetExplicitNestingChildren(null, true))
        {
            RemoveDatabaseObject(explicitNestingChild);
        }
    }
}
