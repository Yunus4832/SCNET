using EntitySystem.TemplatesDatabase;

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class MovingBlockPackage : IPackage
{
    public enum EventType
    {
        Add,
        Remove,
        Stopped,
        Update,
        PistonSound
    }

    public ValuesDictionary? AddData;

    public EventType Type;

    public int NetworkId;

    public uint StateTick;

    public Vector3 Position;

    public Vector3 Velocity;

    public float Speed;

    public byte ID => (byte)PackageType.MovingBlockSet;




    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public MovingBlockPackage()
    {
    }

    public MovingBlockPackage(SubsystemMovingBlocks.MovingBlockSet movingSet)
    {
        Type = EventType.Add;
        NetworkId = movingSet.NetworkId;
        AddData = SubsystemMovingBlocks.SaveMovingItem(movingSet);
    }

    public MovingBlockPackage(SubsystemMovingBlocks.MovingBlockSet movingSet, bool stop)
    {
        Type = stop ? EventType.Stopped : EventType.Remove;
        NetworkId = movingSet.NetworkId;
        Position = movingSet.Position;
        Velocity = movingSet.CurrentVelocity;
        Speed = movingSet.Speed;
    }


    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(Type);
        writer.Write(NetworkId);
        writer.Write(StateTick);
        if (Type == EventType.PistonSound)
        {
            writer.Write(Position);
        }
        if (Type is EventType.Update or EventType.Stopped)
        {
            writer.Write(Position);
            writer.Write(Velocity);
            writer.Write(Speed);
        }
        if (Type == EventType.Add && AddData != null)
        {
            writer.Write(AddData);
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        Type = reader.ReadEnum<EventType>();
        NetworkId = reader.ReadInt32();
        StateTick = reader.ReadUInt32();
        if (Type == EventType.PistonSound)
        {
            Position = reader.ReadVector3();
        }
        if (Type is EventType.Update or EventType.Stopped)
        {
            Position = reader.ReadVector3();
            Velocity = reader.ReadVector3();
            Speed = reader.ReadSingle();
        }
        if (Type == EventType.Add)
        {
            AddData = reader.ReadValuesDictionary();
        }
    }
}
