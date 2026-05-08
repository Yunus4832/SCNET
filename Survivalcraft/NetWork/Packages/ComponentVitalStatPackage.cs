namespace Game.NetWork.Packages;

public class ComponentVitalStatPackage : IPackage
{
    public enum EventType
    {
        SyncStat,
        RequestEat
    }

    public float Food;

    private ushort _entityId;

    private EventType _eventType;

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
        _eventType = EventType.SyncStat;
        _entityId = vitalStats.Entity.EntityId;
        Food = vitalStats.Food;
        Sleep = vitalStats.Sleep;
        Stamina = vitalStats.Stamina;
        Wetness = vitalStats.Wetness;
        Temperature = vitalStats.Temperature;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(_entityId);
        writer.WriteEnum(_eventType);
        switch (_eventType)
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
        _entityId = reader.ReadUInt16();
        _eventType = reader.ReadEnum<EventType>();
        switch (_eventType)
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

    public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
    {
        switch (_eventType)
        {
            case EventType.SyncStat:
                projectNet.FindEntityById(_entityId, entity =>
                {
                    var vitalStats = entity.FindComponent<ComponentVitalStats>();
                    if (vitalStats == null)
                    {
                        return;
                    }

                    if (isServer)
                    {
                        //服务器只同步耐力
                        vitalStats.Stamina = Stamina;
                    }
                    else
                    {
                        //客户端不同步耐力
                        vitalStats.Food = Food;
                        vitalStats.Sleep = Sleep;
                        vitalStats.Wetness = Wetness;
                        vitalStats.Temperature = Temperature;
                    }
                });
                break;
        }
    }
}
