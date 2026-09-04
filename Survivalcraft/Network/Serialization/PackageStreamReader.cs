using System.Xml.Linq;

using Engine.Graphics;
using Engine.Media;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using EntitySystem.XmlUtilities;

namespace Game.Network.Serialization;

public sealed class PackageStreamReader : BinaryReader
{
    public PackageStreamReader(byte[] compressedData) : base(new MemoryStream())
    {
        if (compressedData.Length == 0)
        {
            return;
        }

        var data = CommonLib.DecodeFrame(compressedData);
        BaseStream.Write(data, 0, data.Length);
        BaseStream.Position = 0L;
    }

    public float ReadSmallFloat()
    {
        var d = ReadBytes(2);
        return HalfFloat.HalfToFloat(d);
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

    public Vector3 ReadSmallVec3()
    {
        return new Vector3(ReadSmallFloat(), ReadSmallFloat(), ReadSmallFloat());
    }

    public Vector3? ReadVector3Nullable()
    {
        if (ReadBoolean())
        {
            return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
        }

        return null;
    }

    public Vector3? ReadSmallVector3Nullable()
    {
        if (ReadBoolean())
        {
            return new Vector3(ReadSmallFloat(), ReadSmallFloat(), ReadSmallFloat());
        }

        return null;
    }

    public Vector2 ReadVector2()
    {
        return new Vector2(ReadSingle(), ReadSingle());
    }

    public Vector2 ReadSmallVec2()
    {
        return new Vector2(ReadSmallFloat(), ReadSmallFloat());
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

    public Quaternion ReadSmallQuaternion()
    {
        return new Quaternion(ReadSmallFloat(), ReadSmallFloat(), ReadSmallFloat(), ReadSmallFloat());
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

    public CellFace ReadCellFace()
    {
        var point = ReadBlockPoint();
        return new CellFace(point.X, point.Y, point.Z, ReadInt32());
    }

    public TerrainRaycastResult ReadTerrainRaycastResult()
    {
        var raycast = new TerrainRaycastResult();
        raycast.Ray = ReadRay3();
        raycast.Value = ReadInt32();
        raycast.CollisionBoxIndex = ReadInt32();
        raycast.Distance = ReadSingle();
        raycast.CellFace = ReadCellFace();
        return raycast;
    }

    public TerrainRaycastResult? ReadTerrainRaycastResultNullable()
    {
        if (ReadBoolean())
        {
            return ReadTerrainRaycastResult();
        }

        return null;
    }

    public ValuesDictionary ReadValuesDictionary()
    {
        var data = ReadBytes(ReadInt32());
        var dict = new ValuesDictionary();
        dict.ApplyOverridesUseMessagePack(data);
        return dict;
    }

    public byte[] ReadBuff()
    {
        var buffSize = ReadInt32();
        var buff = new byte[buffSize];
        _ = Read(buff, 0, buffSize);
        return buff;
    }

    public Color ReadColor()
    {
        return new Color(ReadByte(), ReadByte(), ReadByte(), ReadByte());
    }

    public ValuesDictionary ReadPlayerData()
    {
        return ReadValuesDictionary();
    }

    public T ReadEnum<T>() where T : struct
    {
        return (T)Enum.ToObject(typeof(T), (int)ReadByte());
    }

    public XElement ReadXElement()
    {
        var str = ReadString();
        return XmlUtils.LoadXmlFromString(str, true);
    }

    public List<Entity> ReadEntityLoadList()
    {
        if (GameManager.Project is null)
        {
            throw new InvalidOperationException("GameManager.Project is not initialized");
        }

        var messagePack = ReadBuff();
        var vd = new ValuesDictionary();
        vd.ApplyOverridesUseMessagePack(messagePack);
        var entityDataList = new EntityDataList(GameManager.Project.GameDatabase, vd, false);
        return GameManager.Project.LoadEntitiesAll(entityDataList);
    }

    public Texture2D ReadTexture2D()
    {
        var width = ReadInt32();
        var height = ReadInt32();
        var image = new Image(width, height);
        for (var i = 0; i < width; i++)
        {
            for (var j = 0; j < height; j++)
            {
                image.SetPixel(i, j, ReadColor());
            }
        }

        return Texture2D.Load(image);
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
}
