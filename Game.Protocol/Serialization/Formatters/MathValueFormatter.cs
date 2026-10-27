using Engine.Core.Mathematics;

using MessagePack;
using MessagePack.Formatters;

namespace Game.Protocol.Serialization.Formatters;

internal sealed class MathValueFormatter : IMessagePackFormatter<Vector2>, IMessagePackFormatter<Vector3>,
    IMessagePackFormatter<Vector4>, IMessagePackFormatter<Quaternion>, IMessagePackFormatter<Matrix>,
    IMessagePackFormatter<Point2>, IMessagePackFormatter<Point3>, IMessagePackFormatter<Color>,
    IMessagePackFormatter<Ray3>
{
    public static readonly MathValueFormatter Instance = new();

    void IMessagePackFormatter<Vector2>.Serialize(
        ref MessagePackWriter writer,
        Vector2 value,
        MessagePackSerializerOptions options
    )
    {
        writer.WriteArrayHeader(2);
        writer.Write(value.X);
        writer.Write(value.Y);
    }

    Vector2 IMessagePackFormatter<Vector2>.Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options
    )
    {
        if (reader.ReadArrayHeader() != 2) { throw new MessagePackSerializationException("Invalid Vector2 length."); }

        return new Vector2(reader.ReadSingle(), reader.ReadSingle());
    }

    void IMessagePackFormatter<Vector3>.Serialize(
        ref MessagePackWriter writer,
        Vector3 value,
        MessagePackSerializerOptions options
    )
    {
        writer.WriteArrayHeader(3);
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    Vector3 IMessagePackFormatter<Vector3>.Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options
    )
    {
        if (reader.ReadArrayHeader() != 3) { throw new MessagePackSerializationException("Invalid Vector3 length."); }

        return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }

    void IMessagePackFormatter<Vector4>.Serialize(
        ref MessagePackWriter writer,
        Vector4 value,
        MessagePackSerializerOptions options
    )
    {
        writer.WriteArrayHeader(4);
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
        writer.Write(value.W);
    }

    Vector4 IMessagePackFormatter<Vector4>.Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options
    )
    {
        if (reader.ReadArrayHeader() != 4) { throw new MessagePackSerializationException("Invalid Vector4 length."); }

        return new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }

    void IMessagePackFormatter<Quaternion>.Serialize(
        ref MessagePackWriter writer,
        Quaternion value,
        MessagePackSerializerOptions options
    )
    {
        writer.WriteArrayHeader(4);
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
        writer.Write(value.W);
    }

    Quaternion IMessagePackFormatter<Quaternion>.Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options
    )
    {
        if (reader.ReadArrayHeader() != 4)
        {
            throw new MessagePackSerializationException("Invalid Quaternion length.");
        }

        return new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }

    void IMessagePackFormatter<Matrix>.Serialize(
        ref MessagePackWriter writer,
        Matrix value,
        MessagePackSerializerOptions options
    )
    {
        writer.WriteArrayHeader(16);
        writer.Write(value.M11);
        writer.Write(value.M12);
        writer.Write(value.M13);
        writer.Write(value.M14);
        writer.Write(value.M21);
        writer.Write(value.M22);
        writer.Write(value.M23);
        writer.Write(value.M24);
        writer.Write(value.M31);
        writer.Write(value.M32);
        writer.Write(value.M33);
        writer.Write(value.M34);
        writer.Write(value.M41);
        writer.Write(value.M42);
        writer.Write(value.M43);
        writer.Write(value.M44);
    }

    Matrix IMessagePackFormatter<Matrix>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.ReadArrayHeader() != 16) { throw new MessagePackSerializationException("Invalid Matrix length."); }

        return new Matrix(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle()
        );
    }

    void IMessagePackFormatter<Point2>.Serialize(
        ref MessagePackWriter writer,
        Point2 value,
        MessagePackSerializerOptions options
    )
    {
        writer.WriteArrayHeader(2);
        writer.Write(value.X);
        writer.Write(value.Y);
    }

    Point2 IMessagePackFormatter<Point2>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.ReadArrayHeader() != 2) { throw new MessagePackSerializationException("Invalid Point2 length."); }

        return new Point2(reader.ReadInt32(), reader.ReadInt32());
    }

    void IMessagePackFormatter<Point3>.Serialize(
        ref MessagePackWriter writer,
        Point3 value,
        MessagePackSerializerOptions options
    )
    {
        writer.WriteArrayHeader(3);
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    Point3 IMessagePackFormatter<Point3>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.ReadArrayHeader() != 3) { throw new MessagePackSerializationException("Invalid Point3 length."); }

        return new Point3(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
    }

    void IMessagePackFormatter<Color>.Serialize(
        ref MessagePackWriter writer,
        Color value,
        MessagePackSerializerOptions options
    )
    {
        writer.WriteArrayHeader(4);
        writer.Write(value.R);
        writer.Write(value.G);
        writer.Write(value.B);
        writer.Write(value.A);
    }

    Color IMessagePackFormatter<Color>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.ReadArrayHeader() != 4) { throw new MessagePackSerializationException("Invalid Color length."); }

        return new Color(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
    }

    void IMessagePackFormatter<Ray3>.Serialize(
        ref MessagePackWriter writer,
        Ray3 value,
        MessagePackSerializerOptions options
    )
    {
        writer.WriteArrayHeader(6);
        writer.Write(value.Position.X);
        writer.Write(value.Position.Y);
        writer.Write(value.Position.Z);
        writer.Write(value.Direction.X);
        writer.Write(value.Direction.Y);
        writer.Write(value.Direction.Z);
    }

    Ray3 IMessagePackFormatter<Ray3>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.ReadArrayHeader() != 6) { throw new MessagePackSerializationException("Invalid Ray3 length."); }

        return new Ray3(
            new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
            new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())
        );
    }
}
