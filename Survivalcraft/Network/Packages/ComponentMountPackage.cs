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

    private int _fromId;

    private int _targetId;

    private EventType _type;

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
        _type = isRequest ? EventType.MountRequest : EventType.Mount;
        _fromId = rider.Entity.EntityId;
        _targetId = mount.Entity.EntityId;
    }

    public ComponentMountPackage(ComponentRider rider, bool isRequest = false)
    {
        _type = isRequest ? EventType.DismountRequest : EventType.Dismount;
        _fromId = rider.Entity.EntityId;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(_type);
        writer.Write(_fromId);
        writer.Write(_targetId);
    }

    public void ReadData(PackageStreamReader reader)
    {
        _type = reader.ReadEnum<EventType>();
        _fromId = reader.ReadInt32();
        _targetId = reader.ReadInt32();
    }

    public void Handle(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        switch (_type)
        {
            case EventType.Dismount:
                project.FindEntityById(_fromId, entity =>
                {
                    var rider = entity.FindComponent<ComponentRider>();
                    if (rider is null)
                    {
                        return;
                    }

                    //rider是骑乘者
                    rider.StartNetDismounting();
                    if (isServer || rider.Mount == null)
                    {
                        return;
                    }

                    //禁用骑乘生物的组件行为
                    var select = rider.Mount.Entity.FindComponent<ComponentBehaviorSelector>();
                    select?.IsDisableBehavior = true;
                });
                break;
            case EventType.DismountRequest:
                project.FindEntityById(_fromId, entity =>
                {
                    var rider = entity.FindComponent<ComponentRider>();
                    rider?.StartDismounting();
                });
                break;
            case EventType.Mount:
                project.FindEntityById(_fromId, entity =>
                {
                    var rider = entity.FindComponent<ComponentRider>();
                    if (rider is null)
                    {
                        return;
                    }

                    project.FindEntityById(_targetId, entity2 =>
                    {
                        var mount = entity2.FindComponent<ComponentMount>();
                        if (mount == null)
                        {
                            return;
                        }

                        rider.StartNetMounting(mount);
                        if (isServer)
                        {
                            return;
                        }

                        //启动骑乘生物的组件行为
                        var select = mount.Entity.FindComponent<ComponentBehaviorSelector>();
                        select?.IsDisableBehavior = false;
                    });
                });
                break;
            case EventType.MountRequest:
                project.FindEntityById(_fromId, entity =>
                {
                    var rider = entity.FindComponent<ComponentRider>();
                    project.FindEntityById(_targetId, entity2 =>
                    {
                        var mount = entity2.FindComponent<ComponentMount>();
                        if (mount != null && rider != null)
                        {
                            rider.StartMounting(mount);
                        }
                    });
                });
                break;
        }
    }
}
