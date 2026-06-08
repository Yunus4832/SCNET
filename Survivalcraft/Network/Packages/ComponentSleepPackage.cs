using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ComponentSleepPackage : IPackage
{
    public enum EventType
    {
        SleepRequest,
        Sleep,
        WakeupRequest,
        WakeUp
    }

    public bool AllowManualWakeup;

    public int EntityId;

    public bool Result;

    public EventType Type;

    public string Reason = string.Empty;

    public ComponentSleepPackage()
    {
    }

    public ComponentSleepPackage(ComponentSleep sleep, EventType eventType, bool allow = false, bool result = false,
        string msg = "")
    {
        Reason = msg;
        EntityId = sleep.Entity.EntityId;
        Type = eventType;
        AllowManualWakeup = allow;
        Result = result;
    }

    public byte ID => (byte)PackageType.ComponentSleep;
    public Client? To { get; set; }
    public Client? Except { get; set; }
    public Client? From { get; set; }
    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(EntityId);
        writer.WriteEnum(Type);
        writer.Write(AllowManualWakeup);
        writer.Write(Result);
        writer.Write(Reason);
    }

    public void ReadData(PackageStreamReader reader)
    {
        EntityId = reader.ReadInt32();
        Type = reader.ReadEnum<EventType>();
        AllowManualWakeup = reader.ReadBoolean();
        Result = reader.ReadBoolean();
        Reason = reader.ReadString();
    }


}
