using EntitySystem.TemplatesDatabase;

using LiteNetLib.Utils;

namespace Game.Network.Serialization;

public static class SerializationManager
{
    public static void PutIntNullable(this NetDataWriter w, int? num)
    {
        w.Put(num.HasValue);
        if (num.HasValue)
        {
            w.Put(num.Value);
        }
    }

    public static int? GetIntNullable(this NetDataReader r)
    {
        if (r.GetBool())
        {
            return r.GetInt();
        }

        return null;
    }

    public static void PutGuid(this NetDataWriter w, Guid guid)
    {
        w.Put(guid.ToByteArray());
    }

    public static Guid GetGuid(this NetDataReader r)
    {
        var buffer = new byte[16];
        r.GetBytes(buffer, 16);
        return new Guid(buffer);
    }

    public static void PutPoint2(this NetDataWriter w, Point2 point)
    {
        w.Put(point.X);
        w.Put(point.Y);
    }

    public static Point2 GetPoint2(this NetDataReader r)
    {
        var point = Point2.Zero;
        point.X = r.GetInt();
        point.Y = r.GetInt();
        return point;
    }

    public static void PutPoint3(this NetDataWriter w, Point3 point)
    {
        w.Put(point.X);
        w.Put(point.Y);
        w.Put(point.Z);
    }

    public static Point3 GetPoint3(this NetDataReader r)
    {
        var point = Point3.Zero;
        point.X = r.GetInt();
        point.Y = r.GetInt();
        point.Z = r.GetInt();
        return point;
    }

    public static void PutTerrainPoint3(this NetDataWriter w, Point3 point)
    {
        w.Put(point.X);
        w.Put((byte)point.Y);
        w.Put(point.Z);
    }

    public static Point3 GetTerrainPoint3(this NetDataReader r)
    {
        var point = Point3.Zero;
        point.X = r.GetInt();
        point.Y = r.GetByte();
        point.Z = r.GetInt();
        return point;
    }

    public static void PutCellFace(this NetDataWriter w, CellFace cellFace)
    {
        w.Put(cellFace.X);
        w.Put(cellFace.Y);
        w.Put(cellFace.Z);
        w.Put((byte)cellFace.Face);
    }

    public static CellFace GetCellFace(this NetDataReader r)
    {
        return new CellFace(r.GetInt(), r.GetInt(), r.GetInt(), r.GetByte());
    }

    public static void PutStreamWithLength(this NetDataWriter w, Stream s)
    {
        w.PutBytesWithLength(StreamUtils.ReadBytes(s));
    }

    public static void PutValuesDictionary(this NetDataWriter w, ValuesDictionary values)
    {
        w.PutBytesWithLength(values.DatabaseObject.Guid.ToByteArray());
        w.Put(values.DatabaseObject.Name);
        w.Put(CommonLib.SerializeVDict(values));
    }

    public static ValuesDictionary GetValuesDictionary(this NetDataReader r)
    {
        var type = DatabaseManager.GameDatabase.Database.FindDatabaseObjectType(r.GetString(), true);
        var guid = r.GetBytesWithLength();
        var databaseObject = DatabaseManager.GameDatabase.Database.FindDatabaseObject(new Guid(guid), type, true)!;
        var values = CommonLib.ReadVDict(r.GetString());
        values.DatabaseObject = databaseObject;
        return values;
    }

    public static void PutColor(this NetDataWriter w, Color color)
    {
        w.Put(color.R);
        w.Put(color.G);
        w.Put(color.B);
        w.Put(color.A);
    }

    public static Color GetColor(this NetDataReader r)
    {
        return new Color(r.GetByte(), r.GetByte(), r.GetByte(), r.GetByte());
    }
}
