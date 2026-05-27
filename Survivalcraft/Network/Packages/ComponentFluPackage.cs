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

    private int _entityId;

    private float _coughDuration;

    private EventType _eventType;

    private float _fluDuration;

    private float _fluOnset;

    private float _sneezeDuration;

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
        _entityId = flu.Entity.EntityId;
        _eventType = eventType;
        _fluOnset = flu.FluOnset;
        _fluDuration = flu.FluDuration;
        _coughDuration = flu.CoughDuration;
        _sneezeDuration = flu.SneezeDuration;
    }


    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(_entityId);
        writer.WriteEnum(_eventType);
        writer.Write(_fluOnset);
        writer.Write(_fluDuration);
        writer.Write(_coughDuration);
        writer.Write(_sneezeDuration);
    }

    public void ReadData(PackageStreamReader reader)
    {
        _entityId = reader.ReadInt32();
        _eventType = reader.ReadEnum<EventType>();
        _fluOnset = reader.ReadSingle();
        _fluDuration = reader.ReadSingle();
        _coughDuration = reader.ReadSingle();
        _sneezeDuration = reader.ReadSingle();
    }

    public void Handle(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        switch (_eventType)
        {
            case EventType.SyncStat:
                project.FindEntityById(_entityId, entity =>
                {
                    var flu = entity.FindComponent<ComponentFlu>();
                    if (flu == null)
                    {
                        return;
                    }

                    flu.FluOnset = _fluOnset;
                    flu.SneezeDuration = _sneezeDuration;
                    flu.CoughDuration = _coughDuration;
                    flu.FluDuration = _fluDuration;
                });
                break;
            case EventType.FluEffect:
                project.FindEntityById(_entityId, entity =>
                {
                    var flu = entity.FindComponent<ComponentFlu>();
                    if (flu == null)
                    {
                        return;
                    }

                    flu.FluOnset = _fluOnset;
                    flu.SneezeDuration = _sneezeDuration;
                    flu.CoughDuration = _coughDuration;
                    flu.FluDuration = _fluDuration;
                    flu.FluEffect();
                });
                break;
            case EventType.StartFlu:
                project.FindEntityById(_entityId, entity =>
                {
                    var flu = entity.FindComponent<ComponentFlu>();
                    if (flu == null)
                    {
                        return;
                    }

                    flu.FluOnset = _fluOnset;
                    flu.SneezeDuration = _sneezeDuration;
                    flu.CoughDuration = _coughDuration;
                    flu.FluDuration = _fluDuration;
                    flu.StartFlu();
                });
                break;
            case EventType.Sneeze:
                project.FindEntityById(_entityId, entity =>
                {
                    var flu = entity.FindComponent<ComponentFlu>();
                    if (flu == null)
                    {
                        return;
                    }

                    flu.FluOnset = _fluOnset;
                    flu.SneezeDuration = _sneezeDuration;
                    flu.CoughDuration = _coughDuration;
                    flu.FluDuration = _fluDuration;
                    flu.Sneeze();
                });
                break;
        }
    }
}
