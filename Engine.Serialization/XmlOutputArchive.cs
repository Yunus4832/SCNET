using System.Globalization;
using System.Xml.Linq;

namespace Engine.Serialization;

public class XmlOutputArchive(XElement node, int version = 0) : OutputArchive(version)
{
    public XmlOutputArchive(string rootNodeName, int version = 0)
        : this(new XElement(rootNodeName), version)
    {
    }

    public XElement Node { get; private set; } = node;

    public override void Serialize(string? name, sbyte value)
    {
        Serialize(name, value.ToString(CultureInfo.InvariantCulture));
    }

    public override void Serialize(string? name, byte value)
    {
        Serialize(name, value.ToString(CultureInfo.InvariantCulture));
    }

    public override void Serialize(string? name, short value)
    {
        Serialize(name, value.ToString(CultureInfo.InvariantCulture));
    }

    public override void Serialize(string? name, ushort value)
    {
        Serialize(name, value.ToString(CultureInfo.InvariantCulture));
    }

    public override void Serialize(string? name, int value)
    {
        Serialize(name, value.ToString(CultureInfo.InvariantCulture));
    }

    public override void Serialize(string? name, uint value)
    {
        Serialize(name, value.ToString(CultureInfo.InvariantCulture));
    }

    public override void Serialize(string? name, long value)
    {
        Serialize(name, value.ToString(CultureInfo.InvariantCulture));
    }

    public override void Serialize(string? name, ulong value)
    {
        Serialize(name, value.ToString(CultureInfo.InvariantCulture));
    }

    public override void Serialize(string? name, float value)
    {
        Serialize(name, value.ToString("R", CultureInfo.InvariantCulture));
    }

    public override void Serialize(string? name, double value)
    {
        Serialize(name, value.ToString("R", CultureInfo.InvariantCulture));
    }

    public override void Serialize(string? name, bool value)
    {
        Serialize(name, value ? "True" : "False");
    }

    public override void Serialize(string? name, char value)
    {
        Serialize(name, value.ToString());
    }

    public override void Serialize(string? name, string value)
    {
        if (name == null)
        {
            Node.SetValue(value);
        }
        else
        {
            Node.SetAttributeValue(name, value);
        }
    }

    public override void Serialize(string? name, byte[] value)
    {
        Serialize(name, Convert.ToBase64String(value));
    }

    public override void Serialize(string? name, int length, byte[] value)
    {
        if (value.Length != length)
        {
            throw new InvalidOperationException("Invalid fixed array length.");
        }

        Serialize(name, Convert.ToBase64String(value));
    }

    public override void Serialize(string? name, Type type, object? value)
    {
        WriteObject(name, GetSerializeData(type, true), value);
    }

    public override void SerializeCollection<T>(string? name, string? itemName, IEnumerable<T?> collection)
        where T : default
    {
        EnterNode(name);
        var serializeData = GetSerializeData(typeof(T), true);
        foreach (var item in collection)
        {
            EnterNode(itemName);
            WriteObject(null, serializeData, item);
            LeaveNode(itemName);
        }

        LeaveNode(name);
    }

    public override void SerializeDictionary<TK, TV>(string? name, IDictionary<TK, TV?> dictionary) where TV : default
    {
        EnterNode(name);
        if (typeof(TK) == typeof(string))
        {
            var serializeData = GetSerializeData(typeof(TV), true);
            foreach (var item in dictionary)
            {
                var name2 = item.Key as string;
                EnterNode(name2);
                WriteObject(null, serializeData, item.Value);
                LeaveNode(name2);
            }
        }
        else
        {
            var serializeData2 = GetSerializeData(typeof(TK), true);
            var serializeData3 = GetSerializeData(typeof(TV), true);
            foreach (var item2 in dictionary)
            {
                EnterNode("e");
                WriteObject("k", serializeData2, item2.Key);
                WriteObject("v", serializeData3, item2.Value);
                LeaveNode("e");
            }
        }

        LeaveNode(name);
    }

    protected override void WriteObjectInfo(int objectId, bool isReference, Type? runtimeType)
    {
        if (isReference)
        {
            Node.SetAttributeValue("_ref", objectId.ToString(CultureInfo.InvariantCulture));
            return;
        }

        Node.SetAttributeValue("_def", objectId.ToString(CultureInfo.InvariantCulture));
        if (runtimeType != null)
        {
            Node.SetAttributeValue("_type", runtimeType.FullName);
        }
    }

    private void WriteObject(string? name, SerializeData staticSerializeData, object? value)
    {
        if (staticSerializeData.IsHumanReadableSupported)
        {
            Serialize(name, value != null ? HumanReadableConverter.ConvertToString(value) : string.Empty);
            return;
        }

        EnterNode(name);
        base.WriteObject(staticSerializeData, value);
        LeaveNode(name);
    }

    private void EnterNode(string? name)
    {
        if (name == null)
        {
            return;
        }

        var xElement = new XElement(name);
        Node.Add(xElement);
        Node = xElement;
    }

    private void LeaveNode(string? name)
    {
        if (name != null)
        {
            Node = Node.Parent ?? throw new InvalidOperationException("Node.Parent is null");
        }
    }
}
