using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ExplosionsPackage : IPackage
{
    public enum EventType
    {
        Sound,
        Cell
    }

    private Dictionary<Point2, List<(Point3, float)>> _cells = new();

    private float _delay;

    private float _level;

    private Vector3 _position;

    private EventType _type;

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
        _type = EventType.Sound;
        _position = position;
        _level = level;
        _delay = delay;
    }

    public ExplosionsPackage(Dictionary<Point2, List<(Point3, float)>> cells)
    {
        _type = EventType.Cell;
        _cells = new Dictionary<Point2, List<(Point3, float)>>();
        foreach (var c in cells)
        {
            _cells.Add(c.Key, c.Value);
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(_type);
        switch (_type)
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
                writer.Write(_position);
                writer.Write(_level);
                writer.Write(_delay);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _type = reader.ReadEnum<EventType>();
        switch (_type)
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
                _position = reader.ReadVector3();
                _level = reader.ReadSingle();
                _delay = reader.ReadSingle();
                break;
        }
    }

    public void Handle(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var sub = project.FindSubsystem<SubsystemExplosions>(true)!;
        switch (_type)
        {
            case EventType.Cell:
                if (sub.ExplosionParticleSystem == null)
                {
                    break;
                }

                foreach (var i in _cells)
                {
                    foreach (var j in i.Value)
                    {
                        sub.ExplosionParticleSystem.SetExplosionCell(j.Item1, j.Item2);
                    }
                }

                break;
            case EventType.Sound:
                sub.PlayExplosionSound(_position, _level, _delay, true);
                break;
        }
    }
}
