using Engine.Core;

using Game.Protocol.Terrain;

namespace Game.Protocol.Serialization;

public class ProtocolWriter() : BinaryWriter(new MemoryStream())
{
    public byte[] Data(CompressionPolicy compressionPolicy = CompressionPolicy.Adaptive)
    {
        BaseStream.Seek(0, SeekOrigin.Begin);
        return ProtocolFrameCodec.EncodeFrame(BaseStream, compressionPolicy);
    }

    public void WriteNullable(float? v)
    {
        Write(v.HasValue);
        if (v.HasValue)
        {
            Write(v.Value);
        }
    }

    public void WriteNullable(int? v)
    {
        Write(v.HasValue);
        if (v.HasValue)
        {
            Write(v.Value);
        }
    }

    public void Write(Color color)
    {
        Write(color.R);
        Write(color.G);
        Write(color.B);
        Write(color.A);
    }

    public void Write(Vector4? vector)
    {
        Write(vector.HasValue);
        if (vector.HasValue)
        {
            Write(vector.Value);
        }
    }

    public void Write(Vector4 vector)
    {
        Write(vector.X);
        Write(vector.Y);
        Write(vector.Z);
        Write(vector.W);
    }

    public void Write(Vector3 vector)
    {
        Write(vector.X);
        Write(vector.Y);
        Write(vector.Z);
    }

    public void Write(Vector3? vector)
    {
        Write(vector.HasValue);
        if (vector.HasValue)
        {
            Write(vector.Value);
        }
    }

    public void Write(Vector2 vector)
    {
        Write(vector.X);
        Write(vector.Y);
    }

    public void Write(Vector2? vector)
    {
        Write(vector.HasValue);
        if (vector.HasValue)
        {
            Write(vector.Value);
        }
    }

    public void Write(Guid guid)
    {
        var data = guid.ToByteArray();
        Write((byte)data.Length);
        Write(data);
    }

    public void Write(Point3 point)
    {
        Write(point.X);
        Write(point.Y);
        Write(point.Z);
    }

    public void Write(Point2 point)
    {
        Write(point.X);
        Write(point.Y);
    }

    public void Write(Quaternion quaternion)
    {
        Write(quaternion.X);
        Write(quaternion.Y);
        Write(quaternion.Z);
        Write(quaternion.W);
    }

    public void Write(Ray3 ray)
    {
        Write(ray.Position);
        Write(ray.Direction);
    }

    public void Write(Ray3? ray)
    {
        Write(ray.HasValue);
        if (ray.HasValue)
        {
            Write(ray.Value);
        }
    }

    public void WriteBlockPoint(Point3 point)
    {
        Write(point.X);
        Write(point.Y);
        Write(point.Z);
    }

    public void WriteBuff(byte[] buff)
    {
        Write(buff.Length);
        Write(buff);
    }

    public void WriteEnum<T>(T obj) where T : struct
    {
        var b = Convert.ToByte(obj);
        Write(b);
    }

    public void Write(Matrix matrix)
    {
        Write(matrix.M11);
        Write(matrix.M21);
        Write(matrix.M31);
        Write(matrix.M41);

        Write(matrix.M12);
        Write(matrix.M22);
        Write(matrix.M32);
        Write(matrix.M42);

        Write(matrix.M13);
        Write(matrix.M23);
        Write(matrix.M33);
        Write(matrix.M43);

        Write(matrix.M14);
        Write(matrix.M24);
        Write(matrix.M34);
        Write(matrix.M44);
    }

    public void Write(Matrix? matrix)
    {
        if (matrix.HasValue)
        {
            Write(true);
            Write(matrix.Value);
        }
        else
        {
            Write(false);
        }
    }

    public void Write(CellFace cellFace)
    {
        WriteBlockPoint(cellFace.Point);
        Write(cellFace.Face);
    }
}
