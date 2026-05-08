namespace Engine.Serialization;

public abstract class InputArchive(int version) : Archive(version)
{
    private readonly Dictionary<int, object?> _objectById = new();

    public abstract void Serialize(string? name, ref sbyte value);

    public abstract void Serialize(string? name, ref byte value);

    public abstract void Serialize(string? name, ref short value);

    public abstract void Serialize(string? name, ref ushort value);

    public abstract void Serialize(string? name, ref int value);

    public abstract void Serialize(string? name, ref uint value);

    public abstract void Serialize(string? name, ref long value);

    public abstract void Serialize(string? name, ref ulong value);

    public abstract void Serialize(string? name, ref float value);

    public abstract void Serialize(string? name, ref double value);

    public abstract void Serialize(string? name, ref bool value);

    public abstract void Serialize(string? name, ref char value);

    public abstract void Serialize(string? name, ref string value);

    public abstract void Serialize(string? name, ref byte[] value);

    public abstract void Serialize(string? name, int length, ref byte[] value);

    public abstract void Serialize(string? name, Type type, ref object? value);

    public abstract void SerializeCollection<T>(string? name, ICollection<T?> collection);

    public abstract void SerializeDictionary<TK, TV>(string? name, IDictionary<TK, TV?> dictionary) where TK : notnull;

    public void Serialize(string? name, Type type, object value)
    {
        Serialize(name, type, ref value!);
    }

    public void Serialize<T>(string? name, T value) where T : class
    {
        object? value2 = value;
        Serialize(name, typeof(T), ref value2);
    }

    public void Serialize<T>(string? name, ref T? value)
    {
        object? value2 = value;
        Serialize(name, typeof(T), ref value2);
        value = (T?)value2;
    }

    public void Serialize<T>(string? name, Action<T?> setter)
    {
        var value = default(T);
        Serialize(name, ref value);
        setter(value);
    }

    public T? Serialize<T>(string? name)
    {
        var value = default(T);
        Serialize(name, ref value);
        return value;
    }

    public void Serialize(string? name, Type type, Action<object?> setter)
    {
        object? value = null;
        Serialize(name, type, ref value);
        setter(value);
    }

    public object? Serialize(string? name, Type type)
    {
        object? value = null;
        Serialize(name, type, ref value);
        return value;
    }

    public List<T?> SerializeCollection<T>(string? name)
    {
        var list = new List<T?>();
        SerializeCollection(name, list);
        return list;
    }

    public Dictionary<TK, TV?> SerializeDictionary<TK, TV>(string? name) where TK : notnull
    {
        var dictionary = new Dictionary<TK, TV?>();
        SerializeDictionary(name, dictionary);
        return dictionary;
    }

    protected abstract void ReadObjectInfo(out int objectId, out bool isReference, out Type? runtimeType);

    protected virtual void ReadObject(SerializeData staticSerializeData, ref object? value)
    {
        if (!staticSerializeData.UseObjectInfo || !UseObjectInfos)
        {
            ReadObjectWithoutObjectInfo(staticSerializeData, ref value);
        }
        else
        {
            ReadObjectWithObjectInfo(staticSerializeData, ref value);
        }
    }

    private void ReadObjectWithoutObjectInfo(SerializeData staticSerializeData, ref object? value)
    {
        var type = value?.GetType();
        var serializeData = type is not null && !(staticSerializeData.Type == type)
            ? GetSerializeData(type, false)
            : staticSerializeData;
        if (serializeData.AutoConstructObject && value is null)
        {
            value = Activator.CreateInstance(serializeData.Type, true);
        }

        serializeData.Read(this, ref value);
    }

    private void ReadObjectWithObjectInfo(SerializeData staticSerializeData, ref object? value)
    {
        ReadObjectInfo(out var objectId, out var isReference, out var runtimeType);
        if (objectId == 0)
        {
            if (value is not null)
            {
                throw new InvalidOperationException("Serializing null reference into an existing object.");
            }

            return;
        }

        if (isReference)
        {
            if (value is not null)
            {
                throw new InvalidOperationException("Serializing a reference into an existing object.");
            }

            value = _objectById[objectId];
            return;
        }

        var type = value?.GetType();
        SerializeData serializeData;
        if (type is null)
        {
            serializeData = runtimeType is null
                ? staticSerializeData
                : GetSerializeData(runtimeType, false);
        }
        else
        {
            if (runtimeType is not null && runtimeType != type)
            {
                throw new InvalidOperationException("Serialized object has different type than existing object.");
            }

            serializeData = GetSerializeData(type, false);
        }

        if (serializeData.AutoConstructObject && value == null)
        {
            value = Activator.CreateInstance(serializeData.Type, true);
        }

        serializeData.Read(this, ref value);
        _objectById.Add(objectId, value);
    }
}
