using EntitySystem.TemplatesDatabase;

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class MovingBlockPackage : IPackage
{
    [Flags]
    public enum EventType
    {
        Add = 1,
        Remove = 2,
        HagTag = 4,
        Stopped = 8
    }

    public ValuesDictionary? AddData;

    public Point3 Position;

    public EventType Type;

    public string MovingBlockId = string.Empty;

    public byte ID => (byte)PackageType.MovingBlockSet;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public MovingBlockPackage()
    {
    }

    public MovingBlockPackage(SubsystemMovingBlocks.MovingBlockSet movingSet)
    {
        Type = EventType.Add;
        AddData = SubsystemMovingBlocks.SaveMovingItem(movingSet);
    }

    public MovingBlockPackage(IMovingBlockSet movingSet, bool stop)
    {
        Type = EventType.Remove;
        MovingBlockId = movingSet.Id;
        if (stop)
        {
            Type |= EventType.Stopped;
        }

        Type |= EventType.HagTag;
        Position = movingSet.Tag as Point3? ?? new Point3();
    }


    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(Type);
        switch (Type)
        {
            case EventType.Add:
                if (AddData != null)
                {
                    writer.Write(AddData);
                }

                break;
            default:
                writer.Write(MovingBlockId);
                if (Type.HasFlag(EventType.HagTag))
                {
                    writer.Write(Position);
                }

                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        Type = reader.ReadEnum<EventType>();
        switch (Type)
        {
            case EventType.Add:
                AddData = reader.ReadValuesDictionary();
                break;
            default:
                MovingBlockId = reader.ReadString();
                if (Type.HasFlag(EventType.HagTag))
                {
                    Position = reader.ReadPoint3();
                }

                break;
        }
    }


}
