using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ComponentFluPackage : IPackage
{
    public enum EventType
    {
        SyncStat,
        FluEffect,
        StartFlu,
        Sneeze
    }

    public int EntityId;

    public float CoughDuration;

    public EventType PackageEventType;

    public float FluDuration;

    public float FluOnset;

    public float SneezeDuration;

    public byte ID => (byte)PackageType.ComponentFlu;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public ComponentFluPackage()
    {
    }

    public ComponentFluPackage(ComponentFlu flu, EventType eventType)
    {
        EntityId = flu.Entity.EntityId;
        PackageEventType = eventType;
        FluOnset = flu.FluOnset;
        FluDuration = flu.FluDuration;
        CoughDuration = flu.CoughDuration;
        SneezeDuration = flu.SneezeDuration;
    }


    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(EntityId);
        writer.WriteEnum(PackageEventType);
        writer.Write(FluOnset);
        writer.Write(FluDuration);
        writer.Write(CoughDuration);
        writer.Write(SneezeDuration);
    }

    public void ReadData(PackageStreamReader reader)
    {
        EntityId = reader.ReadInt32();
        PackageEventType = reader.ReadEnum<EventType>();
        FluOnset = reader.ReadSingle();
        FluDuration = reader.ReadSingle();
        CoughDuration = reader.ReadSingle();
        SneezeDuration = reader.ReadSingle();
    }


}
