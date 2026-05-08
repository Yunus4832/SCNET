using Engine.Core;

namespace Engine.Serialization;

public class BinaryOutputArchive(Stream stream, int version = 0) : OutputArchive(version), IDisposable
{
    private readonly Dictionary<Type, int> _typeIds = new();

    private int _nextTypeId;

    private EngineBinaryWriter _writer = new(stream);

    public bool Use7BitInts = true;

    public Stream Stream => _writer.BaseStream;

    public void Dispose()
    {
        Utilities.Dispose(ref _writer!);
    }

    public override void Serialize(string? name, sbyte value)
    {
        _writer.Write(value);
    }

    public override void Serialize(string? name, byte value)
    {
        _writer.Write(value);
    }

    public override void Serialize(string? name, short value)
    {
        _writer.Write(value);
    }

    public override void Serialize(string? name, ushort value)
    {
        _writer.Write(value);
    }

    public override void Serialize(string? name, int value)
    {
        if (Use7BitInts)
        {
            _writer.Write7BitEncodedInt(value);
        }
        else
        {
            _writer.Write(value);
        }
    }

    public override void Serialize(string? name, uint value)
    {
        _writer.Write(value);
    }

    public override void Serialize(string? name, long value)
    {
        _writer.Write(value);
    }

    public override void Serialize(string? name, ulong value)
    {
        _writer.Write(value);
    }

    public override void Serialize(string? name, float value)
    {
        _writer.Write(value);
    }

    public override void Serialize(string? name, double value)
    {
        _writer.Write(value);
    }

    public override void Serialize(string? name, bool value)
    {
        _writer.Write(value);
    }

    public override void Serialize(string? name, char value)
    {
        _writer.Write(value);
    }

    public override void Serialize(string? name, string value)
    {
        _writer.Write(value);
    }

    public override void Serialize(string? name, byte[] value)
    {
        _writer.Write7BitEncodedInt(value.Length);
        _writer.Write(value);
    }

    public override void Serialize(string? name, int length, byte[] value)
    {
        if (value.Length != length)
        {
            throw new InvalidOperationException("Invalid fixed array length.");
        }

        _writer.Write(value, 0, length);
    }

    public override void Serialize(string? name, Type type, object? value)
    {
        WriteObject(GetSerializeData(type, true), value);
    }

    public override void SerializeCollection<T>(string? name, string? itemName, IEnumerable<T?> collection)
        where T : default
    {
        var serializeData = GetSerializeData(typeof(T), true);
        var enumerable = collection as T[] ?? collection.ToArray();
        Serialize(null, enumerable.Length);
        foreach (var item in enumerable)
        {
            WriteObject(serializeData, item);
        }
    }

    public override void SerializeDictionary<TK, TV>(string? name, IDictionary<TK, TV?> dictionary) where TV : default
    {
        var serializeData = GetSerializeData(typeof(TK), true);
        var serializeData2 = GetSerializeData(typeof(TV), true);
        Serialize(null, dictionary.Count);
        foreach (var item in dictionary)
        {
            WriteObject(serializeData, item.Key);
            WriteObject(serializeData2, item.Value);
        }
    }

    protected override void WriteObjectInfo(int objectId, bool isReference, Type? runtimeType)
    {
        if (isReference)
        {
            Serialize(null, objectId << 3);
        }
        else if (runtimeType != null)
        {
            if (_typeIds.TryGetValue(runtimeType, out var value))
            {
                Serialize(null, 3 | (objectId << 3));
                Serialize(null, value);
                return;
            }

            value = _nextTypeId++;
            Serialize(null, 7 | (objectId << 3));
            Serialize(null, value);
            Serialize(null, runtimeType.FullName ?? string.Empty);
            _typeIds.Add(runtimeType, value);
        }
        else
        {
            Serialize(null, 1 | (objectId << 3));
        }
    }
}
