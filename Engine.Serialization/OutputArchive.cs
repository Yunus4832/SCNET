namespace Engine.Serialization;

public abstract class OutputArchive(int version) : Archive(version)
{
    private readonly Dictionary<object, int> _idByObject = new();

    private int _nextObjectId = 1;

    public abstract void Serialize(string? name, sbyte value);

    public abstract void Serialize(string? name, byte value);

    public abstract void Serialize(string? name, short value);

    public abstract void Serialize(string? name, ushort value);

    public abstract void Serialize(string? name, int value);

    public abstract void Serialize(string? name, uint value);

    public abstract void Serialize(string? name, long value);

    public abstract void Serialize(string? name, ulong value);

    public abstract void Serialize(string? name, float value);

    public abstract void Serialize(string? name, double value);

    public abstract void Serialize(string? name, bool value);

    public abstract void Serialize(string? name, char value);

    public abstract void Serialize(string? name, string value);

    public abstract void Serialize(string? name, byte[] value);

    public abstract void Serialize(string? name, int length, byte[] value);

    public abstract void Serialize(string? name, Type type, object? value);

    public abstract void SerializeCollection<T>(string? name, string? itemName, IEnumerable<T?> collection);

    public abstract void SerializeDictionary<TK, TV>(string? name, IDictionary<TK, TV?> dictionary) where TK : notnull;

    public void Serialize<T>(string? name, T value)
    {
        Serialize(name, typeof(T), value);
    }

    protected abstract void WriteObjectInfo(int objectId, bool isReference, Type? runtimeType);

    protected virtual void WriteObject(SerializeData staticSerializeData, object? value)
    {
        if (!staticSerializeData.UseObjectInfo || !UseObjectInfos)
        {
            staticSerializeData.Write(this, value);
            return;
        }

        if (value == null)
        {
            WriteObjectInfo(0, true, null);
            return;
        }

        if (_idByObject.TryGetValue(value, out var value2))
        {
            WriteObjectInfo(value2, true, null);
            return;
        }

        value2 = _nextObjectId++;
        _idByObject.Add(value, value2);
        var type = value.GetType();
        if (type == staticSerializeData.Type)
        {
            WriteObjectInfo(value2, false, null);
            staticSerializeData.Write(this, value);
        }
        else
        {
            var serializeData = GetSerializeData(type, false);
            WriteObjectInfo(value2, false, type);
            serializeData.Write(this, value);
        }
    }
}
