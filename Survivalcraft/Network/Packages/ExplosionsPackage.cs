using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ExplosionsPackage : IPackage
{
    public enum EventType
    {
        Sound,
        Cell
    }

    public Dictionary<Point2, List<(Point3, float)>> _cells = new();

    public float Delay;

    public float Level;

    public Vector3 Position;

    public EventType Type;

    public byte ID => (byte)PackageType.Explosion;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public ExplosionsPackage()
    {
    }

    public ExplosionsPackage(Vector3 position, float level, float delay)
    {
        Type = EventType.Sound;
        Position = position;
        Level = level;
        Delay = delay;
    }

    public ExplosionsPackage(Dictionary<Point2, List<(Point3, float)>> cells)
    {
        Type = EventType.Cell;
        _cells = new Dictionary<Point2, List<(Point3, float)>>();
        foreach (var c in cells)
        {
            _cells.Add(c.Key, c.Value);
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(Type);
        switch (Type)
        {
            case EventType.Cell:
                writer.Write(_cells.Count);
                foreach (var c in _cells)
                {
                    writer.Write(c.Key);
                    writer.Write(c.Value.Count);
                    foreach (var cc in c.Value)
                    {
                        writer.Write(cc.Item1);
                        writer.Write(cc.Item2);
                    }
                }

                break;
            case EventType.Sound:
                writer.Write(Position);
                writer.Write(Level);
                writer.Write(Delay);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        Type = reader.ReadEnum<EventType>();
        switch (Type)
        {
            case EventType.Cell:
                _cells = new Dictionary<Point2, List<(Point3, float)>>();
                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var point = reader.ReadPoint2();
                    var list = new List<(Point3, float)>();
                    var cnt = reader.ReadInt32();
                    for (var j = 0; j < cnt; j++)
                    {
                        var point3 = reader.ReadPoint3();
                        var v = reader.ReadSingle();
                        list.Add((point3, v));
                    }

                    _cells.Add(point, list);
                }

                break;
            case EventType.Sound:
                Position = reader.ReadVector3();
                Level = reader.ReadSingle();
                Delay = reader.ReadSingle();
                break;
        }
    }


}
