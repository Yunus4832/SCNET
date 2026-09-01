using Engine.Core;

namespace Engine.Serialization;

public class BinaryInputArchive(Stream stream, int version = 0) : InputArchive(version), IDisposable
{
    private readonly Dictionary<int, Type?> _typeIds = new();

    private EngineBinaryReader _reader = new(stream);

    public bool Use7BitInts = true;

    public Stream Stream => _reader.BaseStream;

    public void Dispose()
    {
        Utilities.Dispose(ref _reader!);
    }

    public override void Serialize(string? name, ref sbyte value)
    {
        value = _reader.ReadSByte();
    }

    public override void Serialize(string? name, ref byte value)
    {
        value = _reader.ReadByte();
    }

    public override void Serialize(string? name, ref short value)
    {
        value = _reader.ReadInt16();
    }

    public override void Serialize(string? name, ref ushort value)
    {
        value = _reader.ReadUInt16();
    }

    public override void Serialize(string? name, ref int value)
    {
        if (Use7BitInts)
        {
            value = _reader.Read7BitEncodedInt();
        }
        else
        {
            value = _reader.ReadInt32();
        }
    }

    public override void Serialize(string? name, ref uint value)
    {
        value = _reader.ReadUInt32();
    }

    public override void Serialize(string? name, ref long value)
    {
        value = _reader.ReadInt64();
    }

    public override void Serialize(string? name, ref ulong value)
    {
        value = _reader.ReadUInt64();
    }

    public override void Serialize(string? name, ref float value)
    {
        value = _reader.ReadSingle();
    }

    public override void Serialize(string? name, ref double value)
    {
        value = _reader.ReadDouble();
    }

    public override void Serialize(string? name, ref bool value)
    {
        value = _reader.ReadBoolean();
    }

    public override void Serialize(string? name, ref char value)
    {
        value = _reader.ReadChar();
    }

    public override void Serialize(string? name, ref string value)
    {
        value = _reader.ReadString();
    }

    public override void Serialize(string? name, ref byte[] value)
    {
        value = new byte[_reader.Read7BitEncodedInt()];
        if (_reader.Read(value, 0, value.Length) != value.Length)
        {
            throw new InvalidOperationException();
        }
    }

    public override void Serialize(string? name, int length, ref byte[] value)
    {
        value = new byte[length];
        if (_reader.Read(value, 0, value.Length) != length)
        {
            throw new InvalidOperationException();
        }
    }

    public override void Serialize(string? name, Type type, ref object? value)
    {
        ReadObject(GetSerializeData(type, true), ref value);
    }

    public override void SerializeCollection<T>(string? name, ICollection<T?> collection) where T : default
    {
        var serializeData = GetSerializeData(typeof(T), true);
        var value = 0;
        Serialize(null, ref value);
        for (var i = 0; i < value; i++)
        {
            object? value2 = null;
            ReadObject(serializeData, ref value2);
            collection.Add((T?)value2);
        }
    }

    public override void SerializeDictionary<TK, TV>(string? name, IDictionary<TK, TV?> dictionary) where TV : default
    {
        var serializeData = GetSerializeData(typeof(TK), true);
        var serializeData2 = GetSerializeData(typeof(TV), true);
        var value = 0;
        Serialize(null, ref value);
        for (var i = 0; i < value; i++)
        {
            object? value2 = null;
            object? value3 = null;
            ReadObject(serializeData, ref value2);
            if (value2 is null)
            {
                throw new InvalidOperationException("Dictionary key can not be null");
            }

            if (dictionary.TryGetValue((TK)value2, out var value4))
            {
                value3 = value4;
            }

            ReadObject(serializeData2, ref value3);
            dictionary.Add((TK)value2, (TV?)value3);
        }
    }

    protected override void ReadObjectInfo(out int objectId, out bool isReference, out Type? runtimeType)
    {
        var value = 0;
        Serialize(null, ref value);
        objectId = value >> 3;
        isReference = (value & 1) == 0;
        if ((value & 2) != 0)
        {
            var value2 = 0;
            Serialize(null, ref value2);
            if ((value & 4) != 0)
            {
                var value3 = string.Empty;
                Serialize(null, ref value3);
                runtimeType = TypeCache.FindType(value3, false, true);
                _typeIds.Add(value2, runtimeType);
            }
            else
            {
                runtimeType = _typeIds[value2];
            }
        }
        else
        {
            runtimeType = null;
        }
    }
}
