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

    private int _attackerEntityId;

    private float _duration;

    private int _entityId;

    private EventType _type;

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
        _type = EventType.ComponentOnFire;
        _duration = duration;
        if (attacker != null)
        {
            _attackerEntityId = attacker.Entity.EntityId;
        }

        _entityId = target.Entity.EntityId;
    }

    public ComponentOnFirePackage(int x, int y, int z, float expandability)
    {
        _type = EventType.BlockOnFireAdd;
        this.X = x;
        this.Y = y;
        this.Z = z;
        _duration = expandability;
    }

    public ComponentOnFirePackage(int x, int y, int z)
    {
        _type = EventType.BlockOnFireRemove;
        this.X = x;
        this.Y = y;
        this.Z = z;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(_type);
        switch (_type)
        {
            case EventType.ComponentOnFire:
                writer.Write(_entityId);
                writer.Write(_attackerEntityId);
                writer.Write(_duration);
                break;
            case EventType.BlockOnFireAdd:
                writer.Write(X);
                writer.Write(Y);
                writer.Write(Z);
                writer.Write(_duration);
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
        _type = reader.ReadEnum<EventType>();
        switch (_type)
        {
            case EventType.ComponentOnFire:
                _entityId = reader.ReadInt32();
                _attackerEntityId = reader.ReadInt32();
                _duration = reader.ReadSingle();
                break;
            case EventType.BlockOnFireAdd:
                X = reader.ReadInt32();
                Y = reader.ReadInt32();
                Z = reader.ReadInt32();
                _duration = reader.ReadSingle();
                break;
            case EventType.BlockOnFireRemove:
                X = reader.ReadInt32();
                Y = reader.ReadInt32();
                Z = reader.ReadInt32();
                break;
        }
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
            case EventType.BlockOnFireAdd:
                project.FindSubsystem<SubsystemFireBlockBehavior>(true)!.AddFireNet(X, Y, Z, _duration);
                break;
            case EventType.BlockOnFireRemove:
                project.FindSubsystem<SubsystemFireBlockBehavior>(true)!.RemoveFireNet(X, Y, Z);
                break;
            case EventType.ComponentOnFire:
                project.FindEntityById(_entityId, e =>
                {
                    var onFire = e.FindComponent<ComponentOnFire>();
                    if (onFire == null)
                    {
                        return;
                    }

                    if (_attackerEntityId == 0)
                    {
                        onFire.SetOnFireNet(null, _duration);
                    }
                    else
                    {
                        project.FindEntityById(_attackerEntityId, e2 =>
                        {
                            var creature = e2.FindComponent<ComponentCreature>();
                            onFire.SetOnFireNet(creature, _duration);
                        });
                    }
                });
                break;
        }
    }
}
