using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ComponentMountPackage : IPackage
{
    public enum EventType
    {
        Mount,
        Dismount,
        MountRequest,
        DismountRequest
    }

    public int FromId;

    public int TargetId;

    public EventType Type;

    public byte ID => (byte)PackageType.ComponentMount;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;


    public ComponentMountPackage()
    {
    }

    public ComponentMountPackage(ComponentRider rider, ComponentMount mount, bool isRequest = false)
    {
        Type = isRequest ? EventType.MountRequest : EventType.Mount;
        FromId = rider.Entity.EntityId;
        TargetId = mount.Entity.EntityId;
    }

    public ComponentMountPackage(ComponentRider rider, bool isRequest = false)
    {
        Type = isRequest ? EventType.DismountRequest : EventType.Dismount;
        FromId = rider.Entity.EntityId;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(Type);
        writer.Write(FromId);
        writer.Write(TargetId);
    }

    public void ReadData(PackageStreamReader reader)
    {
        Type = reader.ReadEnum<EventType>();
        FromId = reader.ReadInt32();
        TargetId = reader.ReadInt32();
    }


}
