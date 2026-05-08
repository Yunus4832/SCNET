namespace EntitySystem.TemplatesDatabase;

public class DatabaseObject
{
    private DatabaseObject? _explicitInheritanceParent;

    private DatabaseObject? _nestingParent;

    public DatabaseObject(
        DatabaseObjectType databaseObjectType,
        string name,
        object? value
    ) : this(databaseObjectType, Guid.NewGuid(), name, value)
    {
    }

    public DatabaseObject(DatabaseObjectType databaseObjectType, string name)
        : this(databaseObjectType, Guid.NewGuid(), name, null)
    {
    }

    public Database? Database { get; set; }

    public DatabaseObjectType Type { get; }

    public Guid Guid { get; }

    public string Name
    {
        get;
        private init
        {
            if (ReadOnly)
            {
                throw new InvalidOperationException("Cannot change name of a read-only database object.");
            }

            if (value == field)
            {
                return;
            }

            if (value.Length > Type.NameLengthLimit)
            {
                throw new InvalidOperationException(
                    $"Name \"{value}\" is too long, maximum name length for database object of type \"{Type.Name}\" is {Type.NameLengthLimit}.");
            }

            if (NestingParent != null)
            {
                foreach (var explicitNestingChild in NestingParent.GetExplicitNestingChildren(null, true))
                {
                    if (explicitNestingChild.Name == value)
                    {
                        throw new InvalidOperationException(
                            $"Database object \"{explicitNestingChild.Name}\" is already nested in parent database object \"{NestingParent.Name}\".");
                    }
                }
            }

            field = value;
        }
    }

    public string Description
    {
        get;
        set
        {
            if (ReadOnly)
            {
                throw new InvalidOperationException("Cannot change description of a read-only database object.");
            }

            field = value ?? throw new ArgumentNullException(nameof(value), "Description cannot be null.");
        }
    } = string.Empty;

    public object Value
    {
        get => Type.SupportsValue ? field : throw new InvalidOperationException("DatabaseObjectType not support value");
        private set
        {
            if (ReadOnly)
            {
                throw new InvalidOperationException("Cannot change value of a read-only database object.");
            }

            if (!Type.SupportsValue)
            {
                throw new InvalidOperationException($"Database objects of type \"{Type.Name}\" do not support values.");
            }

            field = value ?? throw new ArgumentNullException(nameof(value), "Value cannot be null.");
        }
    } = null!;

    public bool ReadOnly { get; }

    public DatabaseObject? NestingParent
    {
        get;
        set
        {
            field = value;
            if (ReadOnly)
            {
                throw new InvalidOperationException("Cannot change nesting parent of a read-only database object.");
            }

            if (value == _nestingParent)
            {
                return;
            }

            if (value != null)
            {
                if (Database != null && Database.Root == this)
                {
                    throw new InvalidOperationException("Root database object cannot be nested.");
                }

                if (!Type.AllowedNestingParents.Contains(value.Type))
                {
                    throw new InvalidOperationException(
                        $"Database object of type {Type.Name} cannot be nested in {value.Type.Name}.");
                }

                if (value == this || value.EffectivelyInheritsFrom(this) || EffectivelyInheritsFrom(value) ||
                    value.IsNestedIn(this))
                {
                    throw new InvalidOperationException("Cannot set nesting parent of database object \"" + Name +
                                                        "\" to database object \"" + value.Name +
                                                        "\" because it would create recursive nesting/inheritance.");
                }

                if (value.FindExplicitNestedChild(Name, null, true, false) != null)
                {
                    throw new InvalidOperationException("Another database object with name \"" + Name +
                                                        "\" is already nested in database object \"" + value.Name +
                                                        "\".");
                }
            }

            if (_nestingParent != null)
            {
                if (!_nestingParent.InternalNestingChildren.Remove(this))
                {
                    throw new InvalidOperationException(
                        "DatabaseObject internal error: nested DatabaseObject not found in container.");
                }

                _nestingParent = null;
                Database?.RemoveDatabaseObject(this);
            }

            if (value == null)
            {
                return;
            }

            value.Database?.AddDatabaseObject(this, true);
            _nestingParent = value;
            _nestingParent.InternalNestingChildren.Add(this);
        }
    }

    public DatabaseObject NestingRoot => _nestingParent == null ? this : _nestingParent.NestingRoot;

    public DatabaseObject? ExplicitInheritanceParent
    {
        get => _explicitInheritanceParent;
        set
        {
            if (ReadOnly)
            {
                throw new InvalidOperationException("Cannot change inheritance parent of a read-only database object.");
            }

            if (value == _explicitInheritanceParent)
            {
                return;
            }

            if (value != null)
            {
                if (value == this || value.EffectivelyInheritsFrom(this) || value.IsNestedIn(this) || IsNestedIn(value))
                {
                    throw new InvalidOperationException("Cannot set inheritance parent of database object \"" + Name +
                                                        "\" to database object \"" + value.Name +
                                                        "\" because it would create recursive nesting/inheritance.");
                }

                if (!Type.AllowedInheritanceParents.Contains(value.Type))
                {
                    throw new InvalidOperationException(
                        $"Database object of type {Type.Name} cannot inherit from {value.Type.Name}.");
                }
            }

            _explicitInheritanceParent = value;
        }
    }

    public DatabaseObject ExplicitInheritanceRoot => _explicitInheritanceParent == null
        ? this
        : _explicitInheritanceParent.ExplicitInheritanceRoot;

    public DatabaseObject? ImplicitInheritanceParent =>
        NestingParent?.EffectiveInheritanceParent?.FindEffectiveNestedChild(Name, null, true, false);

    public DatabaseObject ImplicitInheritanceRoot
    {
        get
        {
            var implicitInheritanceParent = ImplicitInheritanceParent;
            return implicitInheritanceParent == null ? this : implicitInheritanceParent.ImplicitInheritanceRoot;
        }
    }

    public DatabaseObject? EffectiveInheritanceParent => _explicitInheritanceParent ?? ImplicitInheritanceParent;

    public DatabaseObject EffectiveInheritanceRoot
    {
        get
        {
            var effectiveInheritanceParent = EffectiveInheritanceParent;
            return effectiveInheritanceParent == null ? this : effectiveInheritanceParent.EffectiveInheritanceRoot;
        }
    }

    private List<DatabaseObject> InternalNestingChildren { get; } = [];

    public bool IsNestedIn(DatabaseObject databaseObject)
    {
        if (NestingParent == null)
        {
            return false;
        }

        return NestingParent == databaseObject || NestingParent.IsNestedIn(databaseObject);
    }

    /// <summary>
    /// 获取子节点(不包括子节点继承的节点)
    /// </summary>
    /// <param name="type"></param>
    /// <param name="directChildrenOnly"></param>
    /// <returns></returns>
    public IEnumerable<DatabaseObject> GetExplicitNestingChildren(DatabaseObjectType? type, bool directChildrenOnly)
    {
        foreach (var databaseObject in InternalNestingChildren)
        {
            if (type == null || databaseObject.Type == type)
            {
                yield return databaseObject;
            }

            if (directChildrenOnly)
            {
                continue;
            }

            foreach (var explicitNestingChild in databaseObject.GetExplicitNestingChildren(type, false))
            {
                yield return explicitNestingChild;
            }
        }
    }

    public DatabaseObject? FindExplicitNestedChild(
        string name,
        DatabaseObjectType? type,
        bool directChildrenOnly,
        bool throwIfNotFound
    )
    {
        foreach (var explicitNestingChild in GetExplicitNestingChildren(type, directChildrenOnly))
        {
            if (explicitNestingChild.Name == name)
            {
                return explicitNestingChild;
            }
        }

        if (throwIfNotFound)
        {
            throw new InvalidOperationException(
                $"Required database object \"{name}\" not found in database object \"{Name}\"");
        }

        return null;
    }

    public bool ExplicitlyInheritsFrom(DatabaseObject databaseObject)
    {
        var explicitInheritanceParent = ExplicitInheritanceParent;
        if (explicitInheritanceParent == null)
        {
            return false;
        }

        return explicitInheritanceParent == databaseObject ||
               explicitInheritanceParent.ExplicitlyInheritsFrom(databaseObject);
    }

    public bool ImplicitlyInheritsFrom(DatabaseObject databaseObject)
    {
        var implicitInheritanceParent = ImplicitInheritanceParent;
        if (implicitInheritanceParent == null)
        {
            return false;
        }

        return implicitInheritanceParent == databaseObject ||
               implicitInheritanceParent.ImplicitlyInheritsFrom(databaseObject);
    }

    public bool EffectivelyInheritsFrom(DatabaseObject databaseObject)
    {
        var effectiveInheritanceParent = EffectiveInheritanceParent;
        if (effectiveInheritanceParent == null)
        {
            return false;
        }

        return effectiveInheritanceParent == databaseObject ||
               effectiveInheritanceParent.EffectivelyInheritsFrom(databaseObject);
    }

    /// <summary>
    /// 获取子节点包括子节点继承的节点
    /// </summary>
    /// <param name="type"></param>
    /// <param name="directChildrenOnly"></param>
    /// <returns></returns>
    public IEnumerable<DatabaseObject> GetEffectiveNestingChildren(DatabaseObjectType? type, bool directChildrenOnly)
    {
        if (directChildrenOnly)
        {
            foreach (var item in InternalGetEffectiveNestingChildren(new StringBin(), type))
            {
                if (type == null || item.Type == type)
                {
                    yield return item;
                }
            }
        }
        else
        {
            foreach (var databaseObject in GetEffectiveNestingChildren(null, true))
            {
                if (type == null || databaseObject.Type == type)
                {
                    yield return databaseObject;
                }

                foreach (var effectiveNestingChild in databaseObject.GetEffectiveNestingChildren(type, false))
                {
                    yield return effectiveNestingChild;
                }
            }
        }
    }

    public DatabaseObject? FindEffectiveNestedChild(
        string name,
        DatabaseObjectType? type,
        bool directChildrenOnly,
        bool throwIfNotFound
    )
    {
        foreach (var effectiveNestingChild in GetEffectiveNestingChildren(type, directChildrenOnly))
        {
            if (effectiveNestingChild.Name == name)
            {
                return effectiveNestingChild;
            }
        }

        if (throwIfNotFound)
        {
            throw new InvalidOperationException(
                $"Required database object \"{name}\" not found in database object \"{Name}\"");
        }

        return null;
    }

    public T GetNestedValue<T>(string name)
    {
        var databaseObject = FindEffectiveNestedChild(name, Type.NestedValueType, true, true)!;
        return CastValue<T>(databaseObject);
    }

    public T GetNestedValue<T>(string name, T defaultValue)
    {
        var databaseObject = FindEffectiveNestedChild(name, Type.NestedValueType, true, false);
        return databaseObject == null ? defaultValue : CastValue<T>(databaseObject);
    }

    public void SetNestedValue<T>(string name, T value) where T : notnull
    {
        var databaseObject = FindEffectiveNestedChild(name, Type.NestedValueType, true, false);
        if (databaseObject == null || databaseObject.NestingParent != this)
        {
            new DatabaseObject(Type.NestedValueType, Guid.Empty, name, value).NestingParent = this;
        }
        else
        {
            databaseObject.Value = value;
        }
    }

    public override string ToString()
    {
        return NestingParent != null ? $"{Name} in {NestingParent}" : $"{Name}";
    }

    private T CastValue<T>(DatabaseObject databaseObject)
    {
        if (databaseObject.Value != null && databaseObject.Value is not T)
        {
            throw new Exception(
                $"Database object \"{databaseObject.Name}\" has invalid type \"{databaseObject.Value.GetType().FullName}\", required type is \"{typeof(T).FullName}\".");
        }

        return (T)databaseObject.Value!;
    }

    private IEnumerable<DatabaseObject> InternalGetEffectiveNestingChildren(StringBin names, DatabaseObjectType? type)
    {
        if (Type.AllowedNestingChildren.Count == 0)
        {
            yield break;
        }

        foreach (var explicitNestingChild in GetExplicitNestingChildren(type, true))
        {
            if (!names.Contains(explicitNestingChild.Name))
            {
                names.Add(explicitNestingChild.Name);
                yield return explicitNestingChild;
            }
        }

        var effectiveInheritanceParent = EffectiveInheritanceParent;
        if (effectiveInheritanceParent == null)
        {
            yield break;
        }

        foreach (var item in effectiveInheritanceParent.InternalGetEffectiveNestingChildren(names, type))
        {
            yield return item;
        }
    }

    private class StringBin
    {
        private readonly List<string> _list = [];

        private int _mask;

        public bool Contains(string s)
        {
            var num = Hash(s) & 0x1F;
            var num2 = 1 << num;
            return (_mask & num2) != 0 && _list.Contains(s);
        }

        public void Add(string s)
        {
            var num = Hash(s) & 0x1F;
            var num2 = 1 << num;
            _mask |= num2;
            _list.Add(s);
        }

        private static int Hash(string s)
        {
            var length = s.Length;
            return s[0] + s[length >> 1] + s[length - 1];
        }
    }

    public DatabaseObject(
        DatabaseObjectType databaseObjectType,
        Guid guid,
        string name,
        object? value,
        bool readOnly = false
    )
    {
        if (!databaseObjectType.IsInitialized)
        {
            throw new InvalidOperationException(
                $"InitializeRelations of DatabaseObjectType \"{databaseObjectType.Name}\" not called.");
        }

        if (databaseObjectType.SupportsValue && value == null)
        {
            throw new InvalidOperationException(
                $"DatabaseObjectType \"{databaseObjectType.Name}\" SupportsValue is true, value can not be null");
        }

        Type = databaseObjectType;
        Guid = guid;
        Name = name;
        ReadOnly = readOnly;
        if (databaseObjectType.SupportsValue && value != null)
        {
            Value = value;
        }
    }
}
