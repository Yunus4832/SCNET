namespace Game.NetWork.Packages;

public class ComponentSleepPackage : IPackage
{
    public enum EventType
    {
        SleepRequest,
        Sleep,
        WakeupRequest,
        WakeUp
    }

    private bool _allowManualWakeup;

    private ushort _entityId;

    private bool _result;

    private EventType _type;

    private string _reason = string.Empty;

    public ComponentSleepPackage()
    {
    }

    public ComponentSleepPackage(ComponentSleep sleep, EventType eventType, bool allow = false, bool result = false,
        string msg = "")
    {
        _reason = msg;
        _entityId = sleep.Entity.EntityId;
        _type = eventType;
        _allowManualWakeup = allow;
        _result = result;
    }

    public byte ID => (byte)PackageType.ComponentSleep;
    public Client? To { get; set; }
    public Client? Except { get; set; }
    public Client? From { get; set; }
    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(_entityId);
        writer.WriteEnum(_type);
        writer.Write(_allowManualWakeup);
        writer.Write(_result);
        writer.Write(_reason);
    }

    public void ReadData(PackageStreamReader reader)
    {
        _entityId = reader.ReadUInt16();
        _type = reader.ReadEnum<EventType>();
        _allowManualWakeup = reader.ReadBoolean();
        _result = reader.ReadBoolean();
        _reason = reader.ReadString();
    }

    public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
    {
        projectNet.FindEntityById(_entityId, e =>
        {
            var sleep = e.FindComponent<ComponentSleep>();
            if (sleep == null)
            {
                return;
            }

            switch (_type)
            {
                case EventType.SleepRequest:
                    if (sleep.CanSleep(out var reason2))
                    {
                        sleep.Sleep(_allowManualWakeup);
                    }
                    else
                    {
                        netNode.QueuePackage(
                            new ComponentSleepPackage(sleep, EventType.Sleep, _allowManualWakeup, false, reason2)
                                { To = From });
                    }

                    break;
                case EventType.Sleep:
                    if (_result)
                    {
                        sleep.NetSleep(_allowManualWakeup);
                    }
                    else
                    {
                        var player = sleep.Entity.FindComponent<ComponentPlayer>();
                        player?.ComponentGui.DisplaySmallMessage(_reason, Color.White, false, true);
                    }

                    break;
                case EventType.WakeupRequest:
                    sleep.WakeUp();
                    break;
                case EventType.WakeUp:
                    sleep.NetWakeUp();
                    break;
            }
        });
    }
}
