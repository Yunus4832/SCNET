using Engine.Core;

using Game.Protocol.Terrain;

namespace Game.Protocol.Serialization;

public class ProtocolReader : BinaryReader
{
    public ProtocolReader(byte[] frame) : base(new MemoryStream(ProtocolFrameCodec.DecodeFrame(frame), false))
    {
    }

    public ProtocolReader(Stream payload) : base(payload)
    {
    }

    public override byte[] ReadBytes(int count)
    {
        if (count < 0 || count > ProtocolFrameCodec.MaximumPayloadLength ||
            (BaseStream.CanSeek && count > BaseStream.Length - BaseStream.Position))
        {
            throw new InvalidDataException("Invalid or truncated protocol buffer.");
        }
        var bytes = base.ReadBytes(count);
        return bytes.Length == count ? bytes : throw new EndOfStreamException("Truncated protocol buffer.");
    }

    public int ReadCount(int maximum)
    {
        var count = ReadInt32();
        return count >= 0 && count <= maximum ? count : throw new InvalidDataException("Invalid protocol collection count.");
    }

    public float? ReadSingleNullable()
    {
        if (ReadBoolean())
        {
            return ReadSingle();
        }

        return null;
    }

    public int? ReadIntNullable()
    {
        if (ReadBoolean())
        {
            return ReadInt32();
        }

        return null;
    }

    public Vector4 ReadVector4()
    {
        return new Vector4(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
    }

    public Vector4? ReadVector4Nullable()
    {
        if (ReadBoolean())
        {
            return new Vector4(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        return null;
    }

    public Vector3 ReadVector3()
    {
        return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
    }

    public Vector3? ReadVector3Nullable()
    {
        if (ReadBoolean())
        {
            return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
        }

        return null;
    }

    public Vector2 ReadVector2()
    {
        return new Vector2(ReadSingle(), ReadSingle());
    }

    public Vector2? ReadVector2Nullable()
    {
        if (ReadBoolean())
        {
            return new Vector2(ReadSingle(), ReadSingle());
        }

        return null;
    }

    public Guid ReadGuid()
    {
        var len = ReadByte();
        if (len != 16)
        {
            throw new InvalidDataException("A protocol GUID must contain exactly 16 bytes.");
        }
        return new Guid(ReadBytes(len));
    }

    public Point3 ReadPoint3()
    {
        return new Point3(ReadInt32(), ReadInt32(), ReadInt32());
    }

    public Point2 ReadPoint2()
    {
        return new Point2(ReadInt32(), ReadInt32());
    }

    public Quaternion ReadQuaternion()
    {
        return new Quaternion(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
    }

    public Ray3 ReadRay3()
    {
        return new Ray3(ReadVector3(), ReadVector3());
    }

    public Ray3? ReadRay3Nullable()
    {
        if (ReadBoolean())
        {
            return ReadRay3();
        }

        return null;
    }

    public Point3 ReadBlockPoint()
    {
        return new Point3(ReadInt32(), ReadInt32(), ReadInt32());
    }

    public byte[] ReadBuff()
    {
        return ReadBytes(ReadInt32());
    }

    public Color ReadColor()
    {
        return new Color(ReadByte(), ReadByte(), ReadByte(), ReadByte());
    }

    public T ReadEnum<T>() where T : struct
    {
        return (T)Enum.ToObject(typeof(T), (int)ReadByte());
    }

    public Matrix ReadMatrix()
    {
        var matrix = new Matrix
        {
            M11 = ReadSingle(),
            M21 = ReadSingle(),
            M31 = ReadSingle(),
            M41 = ReadSingle(),
            M12 = ReadSingle(),
            M22 = ReadSingle(),
            M32 = ReadSingle(),
            M42 = ReadSingle(),
            M13 = ReadSingle(),
            M23 = ReadSingle(),
            M33 = ReadSingle(),
            M43 = ReadSingle(),
            M14 = ReadSingle(),
            M24 = ReadSingle(),
            M34 = ReadSingle(),
            M44 = ReadSingle()
        };

        return matrix;
    }

    public Matrix? ReadMatrixNullable()
    {
        var f = ReadBoolean();
        if (f)
        {
            return ReadMatrix();
        }

        return null;
    }
    public CellFace ReadCellFace()
    {
        var point = ReadBlockPoint();
        return new CellFace(point.X, point.Y, point.Z, ReadInt32());
    }

}
