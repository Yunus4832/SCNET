using System.Buffers;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

using Engine.Core.Mathematics;

using Game.Protocol.Identity;
using Game.Protocol.Localization;
using Game.Protocol.Messaging;
using Game.Protocol.Terrain;

using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Serialization;

/// <summary>Validates declared MessagePack shapes before generated formatters allocate their object graph.</summary>
internal static class ProtocolShapeGuard
{
    private const int _maximumCollectionLength = ushort.MaxValue;

    private const int _maximumStringBytes = 16 * 1024 * 1024;

    private static readonly UTF8Encoding _utf8 = new(false, true);

    private static readonly Lock _schemaLock = new();

    private static readonly Dictionary<Type, Shape> _shapes = [];

    private static readonly NullabilityInfoContext _nullability = new();

    public static void Validate<T>(ReadOnlyMemory<byte> payload)
    {
        if (payload.Length > ProtocolFrameCodec.MaximumPayloadLength)
        {
            throw new InvalidDataException("Message payload is too large.");
        }

        Shape shape;
        lock (_schemaLock) { shape = Schema(typeof(T)); }

        var reader = new MessagePackReader(payload);
        Visit(ref reader, new Edge(shape, false), 0);
        if (!reader.End) { throw new InvalidDataException("Trailing message data."); }
    }

    private static Shape Schema(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (_shapes.TryGetValue(type, out var found)) { return found; }

        var shape = new Shape();
        _shapes[type] = shape;
        try
        {
            if (type == typeof(string) ||
                type == typeof(Guid) ||
                type == typeof(ModId) ||
                type == typeof(ResourceId) ||
                type == typeof(PlayerAddress) ||
                typeof(JsonNode).IsAssignableFrom(type))
            {
                shape.Kind = Kind.Text;
                shape.Limit = type == typeof(PlayerAddress) ? Engine.Core.Security.IdentityAddress.EncodedLength :
                    type == typeof(ResourceId) ? ProtocolLimits.MaximumResourceIdLength * 4 : _maximumStringBytes;
            }
            else if (type == typeof(byte[]) || type == typeof(ReadOnlyMemory<byte>) || type == typeof(Memory<byte>))
            {
                shape.Kind = Kind.Binary;
            }
            else if (type.IsEnum)
            {
                shape.Kind = Kind.Enum;
                shape.IsFlags = type.IsDefined(typeof(FlagsAttribute), false);
                shape.Unsigned = Enum.GetUnderlyingType(type) == typeof(ulong);
                shape.EnumValues = Enum.GetValues(type).Cast<object>().Select(EnumValue).ToHashSet();
                shape.Mask = shape.EnumValues.Aggregate(0UL, (mask, value) => mask | value);
            }
            else if (type == typeof(bool)) { shape.Kind = Kind.Boolean; }
            else if (type.IsPrimitive || type == typeof(decimal) || type == typeof(TimeSpan))
            {
                shape.Kind = Kind.Number;
            }
            else if (type == typeof(DateTime)) { shape.Kind = Kind.Extension; }
            else if (type == typeof(MessageContent))
            {
                shape.Kind = Kind.List;
                shape.Element = new Edge(Schema(typeof(MessageSegment)), false);
                shape.Limit = 64;
            }
            else if (type == typeof(LocalizedText))
            {
                shape.Kind = Kind.Object;
                shape.Fields = Enumerable.Repeat(new Edge(Schema(typeof(string)), false), 3).ToArray();
            }
            else if (MathWidth(type) is var width && width > 0)
            {
                shape.Kind = Kind.Object;
                shape.Fields = Enumerable.Repeat(new Edge(Schema(typeof(float)), false), width).ToArray();
            }
            else if (type.IsArray)
            {
                shape.Kind = Kind.List;
                shape.Element = new Edge(
                    Schema(type.GetElementType()!),
                    Nullable.GetUnderlyingType(type.GetElementType()!) is not null
                );
            }
            else if (type.IsGenericType &&
                     type.GetGenericTypeDefinition() is var generic &&
                     (generic == typeof(IReadOnlyDictionary<,>) ||
                      generic == typeof(IDictionary<,>) ||
                      generic == typeof(Dictionary<,>)))
            {
                shape.Kind = Kind.Map;
                shape.Limit = ProtocolFrameCodec.MaximumPayloadLength / 16;
                shape.Key = new Edge(Schema(type.GetGenericArguments()[0]), false);
                shape.Element = new Edge(
                    Schema(type.GetGenericArguments()[1]),
                    Nullable.GetUnderlyingType(type.GetGenericArguments()[1]) is not null
                );
            }
            else if (type.IsGenericType &&
                     type.GetGenericTypeDefinition() is var sequence &&
                     (sequence == typeof(IReadOnlyList<>) ||
                      sequence == typeof(IReadOnlyCollection<>) ||
                      sequence == typeof(IList<>) ||
                      sequence == typeof(List<>) ||
                      sequence == typeof(IEnumerable<>)))
            {
                shape.Kind = Kind.List;
                shape.Element = new Edge(
                    Schema(type.GetGenericArguments()[0]),
                    Nullable.GetUnderlyingType(type.GetGenericArguments()[0]) is not null
                );
            }
            else if (type.IsAbstract || type.IsInterface)
            {
                shape.Kind = Kind.Union;
                shape.Unions = type.GetCustomAttributes<UnionAttribute>(false)
                    .ToDictionary(attribute => attribute.Key, attribute => Schema(attribute.SubType));
                if (shape.Unions.Count == 0)
                {
                    throw new InvalidDataException($"No declared wire variants for {type.Name}.");
                }
            }
            else
            {
                if (type.GetCustomAttribute<MessagePackObjectAttribute>() is null)
                {
                    throw new InvalidDataException($"Type {type.Name} has no declared MessagePack contract.");
                }

                shape.Kind = Kind.Object;
                var fields = new Dictionary<int, Edge>();
                foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
                {
                    var key = member.GetCustomAttribute<KeyAttribute>();
                    if (key?.IntKey is not { } index) { continue; }

                    var info = member switch
                    {
                        PropertyInfo property => _nullability.Create(property),
                        FieldInfo field => _nullability.Create(field),
                        _ => null
                    };
                    if (info is null) { continue; }

                    fields.Add(
                        index,
                        MemberEdge(info, member.GetCustomAttribute<MaxLengthAttribute>()?.Length)
                    );
                }

                if (fields.Count > 0 && fields.Keys.Max() != fields.Count - 1)
                {
                    throw new InvalidDataException($"Type {type.Name} has non-contiguous field keys.");
                }

                shape.Fields = fields.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray();
            }

            shape.Complete = true;
            return shape;
        }
        catch
        {
            _shapes.Remove(type);
            throw;
        }
    }

    private static Edge MemberEdge(NullabilityInfo info, int? limit = null)
    {
        var shape = Schema(info.Type);
        if (shape.Kind == Kind.List && (info.ElementType ?? info.GenericTypeArguments.FirstOrDefault()) is { } element)
        {
            shape = new Shape { Kind = Kind.List, Complete = true, Limit = shape.Limit, Element = MemberEdge(element) };
        }
        else if (shape.Kind == Kind.Map && info.GenericTypeArguments.Length == 2)
        {
            shape = new Shape
            {
                Kind = Kind.Map, Complete = true, Limit = shape.Limit,
                Key = MemberEdge(info.GenericTypeArguments[0]), Element = MemberEdge(info.GenericTypeArguments[1])
            };
        }

        return new Edge(shape, AllowsNull(info.Type, info), limit);
    }

    private static bool AllowsNull(Type type, NullabilityInfo info) =>
        Nullable.GetUnderlyingType(type) is not null ||
        !type.IsValueType && info.ReadState == NullabilityState.Nullable;

    private static ulong EnumValue(object value)
    {
        try { return Convert.ToUInt64(value); }
        catch (OverflowException) { return unchecked((ulong)Convert.ToInt64(value)); }
    }

    private static int MathWidth(Type type)
    {
        if (type == typeof(Vector2) || type == typeof(Point2)) { return 2; }

        if (type == typeof(Vector3) || type == typeof(Point3)) { return 3; }

        if (type == typeof(Vector4) || type == typeof(Quaternion) || type == typeof(Color) || type == typeof(CellFace))
        {
            return 4;
        }

        if (type == typeof(Ray3)) { return 6; }

        return type == typeof(Matrix) ? 16 : 0;
    }

    private static void Visit(ref MessagePackReader reader, Edge edge, int depth)
    {
        if (depth > 64) { throw new InvalidDataException("Message graph is too deep."); }

        var shape = edge.Shape;
        if (!shape.Complete) { throw new InvalidDataException("Incomplete protocol schema."); }

        if (reader.TryReadNil())
        {
            if (!edge.Nullable) { throw new InvalidDataException("A required protocol value is missing."); }

            return;
        }

        var remaining = reader.Sequence.Length - reader.Consumed;
        switch (shape.Kind)
        {
            case Kind.Text:
                if (reader.NextMessagePackType != MessagePackType.String)
                {
                    throw new InvalidDataException("Invalid protocol string.");
                }

                var text = reader.ReadStringSequence()!.Value;
                if (text.Length > (edge.Limit ?? shape.Limit))
                {
                    throw new InvalidDataException("Oversized protocol string.");
                }

                _utf8.GetCharCount(text.IsSingleSegment ? text.FirstSpan : text.ToArray());

                break;
            case Kind.Binary:
                if (reader.NextMessagePackType != MessagePackType.Binary ||
                    reader.ReadBytes()!.Value.Length > (edge.Limit ?? ProtocolFrameCodec.MaximumPayloadLength))
                {
                    throw new InvalidDataException("Invalid protocol binary value.");
                }

                break;
            case Kind.Object:
                if (reader.NextMessagePackType != MessagePackType.Array ||
                    reader.ReadArrayHeader() != shape.Fields.Length)
                {
                    throw new InvalidDataException("Protocol fields do not match the declared contract.");
                }

                foreach (var field in shape.Fields) { Visit(ref reader, field, depth + 1); }

                break;
            case Kind.Union:
                if (reader.ReadArrayHeader() != 2) { throw new InvalidDataException("Invalid protocol union."); }

                var tag = reader.ReadInt32();
                if (!shape.Unions.TryGetValue(tag, out var variant))
                {
                    throw new InvalidDataException("Unknown message variant.");
                }

                Visit(ref reader, new Edge(variant, false), depth + 1);
                break;
            case Kind.List:
                var count = reader.ReadArrayHeader();
                if (count > (edge.Limit ?? shape.Limit) || count > remaining)
                {
                    throw new InvalidDataException("Invalid collection length.");
                }

                for (var i = 0; i < count; i++) { Visit(ref reader, shape.Element!, depth + 1); }

                break;
            case Kind.Map:
                var pairs = reader.ReadMapHeader();
                if (pairs > (edge.Limit ?? shape.Limit) || (long)pairs * 2 > remaining)
                {
                    throw new InvalidDataException("Invalid map length.");
                }

                for (var i = 0; i < pairs; i++)
                {
                    Visit(ref reader, shape.Key!, depth + 1);
                    Visit(ref reader, shape.Element!, depth + 1);
                }

                break;
            case Kind.Enum:
                var number = shape.Unsigned ? reader.ReadUInt64() : unchecked((ulong)reader.ReadInt64());
                if (shape.IsFlags ? (number & ~shape.Mask) != 0 : !shape.EnumValues.Contains(number))
                {
                    throw new InvalidDataException("Unknown protocol enum value.");
                }

                break;
            case Kind.Boolean:
                reader.ReadBoolean();
                break;
            case Kind.Number:
                if (reader.NextMessagePackType is not (MessagePackType.Integer or MessagePackType.Float))
                {
                    throw new InvalidDataException("Invalid protocol numeric value.");
                }

                reader.Skip();
                break;
            case Kind.Extension:
                var extension = reader.ReadExtensionFormatHeader();
                if (extension.Length > 16) { throw new InvalidDataException("Invalid timestamp extension."); }

                reader.ReadRaw(extension.Length);
                break;
        }
    }

    private enum Kind
    {
        Text = 0, Binary = 1, Object = 2, Union = 3, List = 4, Map = 5, Enum = 6, Boolean = 7, Number = 8, Extension = 9
    }

    private readonly record struct Edge(Shape Shape, bool Nullable, int? Limit = null);

    private sealed class Shape
    {
        public Kind Kind;

        public bool Complete;

        public int Limit = _maximumCollectionLength;

        public Edge[] Fields = [];

        public Edge Element;

        public Edge Key;

        public Dictionary<int, Shape> Unions = [];

        public HashSet<ulong> EnumValues = [];

        public ulong Mask;

        public bool IsFlags;

        public bool Unsigned;
    }
}
