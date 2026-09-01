using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ComponentOnFirePackage : IPackage
{
    public enum EventType
    {
        ComponentOnFire,
        BlockOnFireAdd,
        BlockOnFireRemove
    }

    public int AttackerEntityId;

    public float Duration;

    public int EntityId;

    public EventType Type;

    public int X;

    public int Y;

    public int Z;

    public byte ID => (byte)PackageType.ComponentOnFire;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public ComponentOnFirePackage()
    {
    }

    public ComponentOnFirePackage(ComponentOnFire target, ComponentCreature? attacker, float duration)
    {
        Type = EventType.ComponentOnFire;
        Duration = duration;
        if (attacker != null)
        {
            AttackerEntityId = attacker.Entity.EntityId;
        }

        EntityId = target.Entity.EntityId;
    }

    public ComponentOnFirePackage(int x, int y, int z, float expandability)
    {
        Type = EventType.BlockOnFireAdd;
        this.X = x;
        this.Y = y;
        this.Z = z;
        Duration = expandability;
    }

    public ComponentOnFirePackage(int x, int y, int z)
    {
        Type = EventType.BlockOnFireRemove;
        this.X = x;
        this.Y = y;
        this.Z = z;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(Type);
        switch (Type)
        {
            case EventType.ComponentOnFire:
                writer.Write(EntityId);
                writer.Write(AttackerEntityId);
                writer.Write(Duration);
                break;
            case EventType.BlockOnFireAdd:
                writer.Write(X);
                writer.Write(Y);
                writer.Write(Z);
                writer.Write(Duration);
                break;
            case EventType.BlockOnFireRemove:
                writer.Write(X);
                writer.Write(Y);
                writer.Write(Z);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        Type = reader.ReadEnum<EventType>();
        switch (Type)
        {
            case EventType.ComponentOnFire:
                EntityId = reader.ReadInt32();
                AttackerEntityId = reader.ReadInt32();
                Duration = reader.ReadSingle();
                break;
            case EventType.BlockOnFireAdd:
                X = reader.ReadInt32();
                Y = reader.ReadInt32();
                Z = reader.ReadInt32();
                Duration = reader.ReadSingle();
                break;
            case EventType.BlockOnFireRemove:
                X = reader.ReadInt32();
                Y = reader.ReadInt32();
                Z = reader.ReadInt32();
                break;
        }
    }
}
