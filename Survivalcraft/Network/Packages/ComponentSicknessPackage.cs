using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ComponentSicknessPackage : IPackage
{
    public enum EventType
    {
        SyncStat,
        NauseaEffect
    }

    public int EntityId;

    public float SicknessDuration;

    public byte ID => (byte)PackageType.ComponentSickness;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public ComponentSicknessPackage()
    {
    }

    public ComponentSicknessPackage(ComponentSickness sickness)
    {
        EntityId = sickness.Entity.EntityId;
        SicknessDuration = sickness.SicknessDuration;
    }


    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(EntityId);
        writer.Write(SicknessDuration);
    }

    public void ReadData(PackageStreamReader reader)
    {
        EntityId = reader.ReadInt32();
        SicknessDuration = reader.ReadSingle();
    }
}
