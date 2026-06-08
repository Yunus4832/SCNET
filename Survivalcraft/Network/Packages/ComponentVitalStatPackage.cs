using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ComponentVitalStatPackage : IPackage
{
    public enum EventType
    {
        SyncStat,
        RequestEat
    }

    public float Food;

    public int EntityId;

    public EventType PackageEventType;

    public float Sleep;

    public float Stamina;

    public float Temperature;

    public float Wetness;

    public byte ID => (byte)PackageType.ComponentVitalStat;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.Playing;

    public ComponentVitalStatPackage()
    {
    }

    public ComponentVitalStatPackage(ComponentVitalStats vitalStats)
    {
        PackageEventType = EventType.SyncStat;
        EntityId = vitalStats.Entity.EntityId;
        Food = vitalStats.Food;
        Sleep = vitalStats.Sleep;
        Stamina = vitalStats.Stamina;
        Wetness = vitalStats.Wetness;
        Temperature = vitalStats.Temperature;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(EntityId);
        writer.WriteEnum(PackageEventType);
        switch (PackageEventType)
        {
            case EventType.SyncStat:
                writer.Write(Food);
                writer.Write(Sleep);
                writer.Write(Wetness);
                writer.Write(Temperature);
                writer.Write(Stamina);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        EntityId = reader.ReadInt32();
        PackageEventType = reader.ReadEnum<EventType>();
        switch (PackageEventType)
        {
            case EventType.SyncStat:
                Food = reader.ReadSingle();
                Sleep = reader.ReadSingle();
                Wetness = reader.ReadSingle();
                Temperature = reader.ReadSingle();
                Stamina = reader.ReadSingle();
                break;
        }
    }


}
