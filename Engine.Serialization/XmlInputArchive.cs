using System.Globalization;
using System.Xml.Linq;

namespace Engine.Serialization;

public class XmlInputArchive(XElement node, int version = 0) : InputArchive(version)
{
    public XElement Node { get; private set; } = node;

    public override void Serialize(string? name, ref sbyte value)
    {
        var value2 = string.Empty;
        Serialize(name, ref value2);
        value = sbyte.Parse(value2, CultureInfo.InvariantCulture);
    }

    public override void Serialize(string? name, ref byte value)
    {
        var value2 = string.Empty;
        Serialize(name, ref value2);
        value = byte.Parse(value2, CultureInfo.InvariantCulture);
    }

    public override void Serialize(string? name, ref short value)
    {
        var value2 = string.Empty;
        Serialize(name, ref value2);
        value = short.Parse(value2, CultureInfo.InvariantCulture);
    }

    public override void Serialize(string? name, ref ushort value)
    {
        var value2 = string.Empty;
        Serialize(name, ref value2);
        value = ushort.Parse(value2, CultureInfo.InvariantCulture);
    }

    public override void Serialize(string? name, ref int value)
    {
        var value2 = string.Empty;
        Serialize(name, ref value2);
        value = int.Parse(value2, CultureInfo.InvariantCulture);
    }

    public override void Serialize(string? name, ref uint value)
    {
        var value2 = string.Empty;
        Serialize(name, ref value2);
        value = uint.Parse(value2, CultureInfo.InvariantCulture);
    }

    public override void Serialize(string? name, ref long value)
    {
        var value2 = string.Empty;
        Serialize(name, ref value2);
        value = long.Parse(value2, CultureInfo.InvariantCulture);
    }

    public override void Serialize(string? name, ref ulong value)
    {
        var value2 = string.Empty;
        Serialize(name, ref value2);
        value = ulong.Parse(value2, CultureInfo.InvariantCulture);
    }

    public override void Serialize(string? name, ref float value)
    {
        var value2 = string.Empty;
        Serialize(name, ref value2);
        value = float.Parse(value2, CultureInfo.InvariantCulture);
    }

    public override void Serialize(string? name, ref double value)
    {
        var value2 = string.Empty;
        Serialize(name, ref value2);
        value = double.Parse(value2, CultureInfo.InvariantCulture);
    }

    public override void Serialize(string? name, ref bool value)
    {
        var value2 = string.Empty;
        Serialize(name, ref value2);
        if (string.Equals(value2, "False", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return;
        }

        if (string.Equals(value2, "True", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return;
        }

        throw new InvalidOperationException($"Cannot convert string \"{value2}\" to a Boolean.");
    }

    public override void Serialize(string? name, ref char value)
    {
        var value2 = string.Empty;
        Serialize(name, ref value2);
        if (value2.Length == 1)
        {
            value = value2[0];
            return;
        }

        throw new InvalidOperationException($"Cannot convert string \"{value2}\" to a Char.");
    }

    public override void Serialize(string? name, ref string value)
    {
        if (name is not null)
        {
            var xAttribute = Node.Attribute(name);
            if (xAttribute == null)
            {
                throw new InvalidOperationException($"Required XML node \"{name}\" not found.");
            }

            value = xAttribute.Value;
        }
        else
        {
            value = Node.Value;
        }
    }

    public override void Serialize(string? name, ref byte[] value)
    {
        var value2 = string.Empty;
        Serialize(name, ref value2);
        value = Convert.FromBase64String(value2);
    }

    public override void Serialize(string? name, int length, ref byte[] value)
    {
        var value2 = string.Empty;
        Serialize(name, ref value2);
        value = Convert.FromBase64String(value2);
        if (value.Length != length)
        {
            throw new InvalidOperationException("Invalid fixed array length.");
        }
    }

    public override void Serialize(string? name, Type type, ref object? value)
    {
        ReadObject(name, GetSerializeData(type, true), ref value);
    }

    public override void SerializeCollection<T>(string? name, ICollection<T?> collection) where T : default
    {
        EnterNode(name);
        var serializeData = GetSerializeData(typeof(T), true);
        using (var enumerator = Node.Elements().GetEnumerator())
        {
            while (enumerator.MoveNext())
            {
                var xElement = Node = enumerator.Current;
                object? value = null;
                ReadObject(null, serializeData, ref value);
                collection.Add((T?)value);
                Node = Node.Parent ?? throw new InvalidOperationException("Node.Parent is null");
            }
        }

        LeaveNode(name);
    }

    public override void SerializeDictionary<TK, TV>(string? name, IDictionary<TK, TV?> dictionary) where TV : default
    {
        EnterNode(name);
        var serializeData = GetSerializeData(typeof(TK), true);
        var serializeData2 = GetSerializeData(typeof(TV), true);
        if (typeof(TK) == typeof(string))
        {
            using var enumerator = Node.Elements().GetEnumerator();
            while (enumerator.MoveNext())
            {
                var xElement = Node = enumerator.Current;
                object localName = xElement.Name.LocalName;
                object? value = null;
                if (dictionary.TryGetValue((TK)localName, out var value2))
                {
                    value = value2;
                    ReadObject(null, serializeData2, ref value);
                }
                else
                {
                    ReadObject(null, serializeData2, ref value);
                    dictionary.Add((TK)localName, (TV?)value);
                }

                Node = Node.Parent ?? throw new InvalidOperationException("Node.Parent is null");
            }
        }
        else
        {
            using var enumerator = Node.Elements().GetEnumerator();
            while (enumerator.MoveNext())
            {
                object? value3 = null;
                object? value4 = null;
                ReadObject("k", serializeData, ref value3);
                if (value3 is null or not TK)
                {
                    throw new InvalidOperationException("Dictoinary key invalid");
                }

                if (dictionary.TryGetValue((TK)value3, out var value5))
                {
                    value4 = value5;
                }

                ReadObject("v", serializeData2, ref value4);
                dictionary.Add((TK)value3, (TV?)value4);
                Node = Node.Parent ?? throw new InvalidOperationException("Node.Parent is null");
            }
        }

        LeaveNode(name);
    }

    protected override void ReadObjectInfo(out int objectId, out bool isReference, out Type? runtimeType)
    {
        var xAttribute = Node.Attribute("_ref");
        if (xAttribute != null)
        {
            runtimeType = null;
            isReference = true;
            objectId = int.Parse(xAttribute.Value);
            return;
        }

        var xAttribute2 = Node.Attribute("_def");
        if (xAttribute2 == null)
        {
            throw new InvalidOperationException("Required XML _ref/_def attribute not found.");
        }

        var xAttribute3 = Node.Attribute("_type");
        runtimeType = xAttribute3 != null ? TypeCache.FindType(xAttribute3.Value, false, true) : null;
        isReference = false;
        objectId = int.Parse(xAttribute2.Value);
    }

    private void ReadObject(string? name, SerializeData staticSerializeData, ref object? value)
    {
        if (staticSerializeData.IsHumanReadableSupported)
        {
            var value2 = string.Empty;
            Serialize(name, ref value2);
            value = HumanReadableConverter.ConvertFromString(staticSerializeData.Type, value2);
        }
        else
        {
            EnterNode(name);
            base.ReadObject(staticSerializeData, ref value);
            LeaveNode(name);
        }
    }

    private void EnterNode(string? name)
    {
        if (name == null)
        {
            return;
        }

        var xElement = Node.Element(name);
        Node = xElement ?? throw new InvalidOperationException($"XML element \"{name}\" not found.");
    }

    private void LeaveNode(string? name)
    {
        if (name != null)
        {
            Node = Node.Parent ?? throw new InvalidOperationException("Node.Parent is null");
        }
    }
}
