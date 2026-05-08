namespace Game.NetWork.Packages;

public class ComponentHealthPackage : IPackage
{
    public enum EventType
    {
        HitResult,
        Injure,
        RequestInjure,
        SyncHealth,
        Damage //船被破坏
    }

    public enum RequestInjureType
    {
        Unknown, // 未知
        Choke, // 自杀
        Cactus, // 仙人掌刺伤
        Fall, // 摔伤
        Fire // 着火
    }

    private float _amount;

    private ushort _attackerId;

    private Color _color;

    public string Cause = string.Empty;

    private float _health;

    private bool _ignoreInvulnerability;

    private Vector3 _position;

    private RequestInjureType _requestInjureType;

    private ushort _targetId;

    private string _text = string.Empty;

    private EventType _type;

    private Vector3 _velocity;

    public byte ID => (byte)PackageType.ComponentHealth;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public ComponentHealthPackage()
    {
    }

    public ComponentHealthPackage(ComponentHealth health)
    {
        _targetId = health.Entity.EntityId;
        _health = health.Health;
        _type = EventType.SyncHealth;
    }

    public ComponentHealthPackage(ComponentDamage health)
    {
        _targetId = health.Entity.EntityId;
        _health = health.HitPoints;
        _type = EventType.Damage;
    }

    public ComponentHealthPackage(ComponentHealth target, ComponentCreature? attacker, float amount, string cuase,
        bool ignoreInvulnerability = false, bool isRequest = false,
        RequestInjureType requestInjureType = RequestInjureType.Unknown)
    {
        _type = isRequest ? EventType.RequestInjure : EventType.Injure;
        _amount = amount;
        _attackerId = attacker == null ? (ushort)0 : attacker.Entity.EntityId;
        _health = target.Health;
        _targetId = target.Entity.EntityId;
        Cause = cuase;
        _ignoreInvulnerability = ignoreInvulnerability;
        _requestInjureType = requestInjureType;
    }

    public ComponentHealthPackage(Vector3 position, Vector3 velocity, Color color, string text)
    {
        _position = position;
        _velocity = velocity;
        _color = color;
        _text = text;
        _type = EventType.HitResult;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(_type);
        switch (_type)
        {
            case EventType.RequestInjure:
                writer.Write(_targetId);
                writer.Write(_attackerId);
                writer.Write(_health);
                writer.Write(_ignoreInvulnerability);
                writer.Write(_amount);
                writer.Write(Cause);
                writer.WriteEnum(_requestInjureType);
                break;
            case EventType.Injure:
                writer.Write(_targetId);
                writer.Write(_attackerId);
                writer.Write(_health);
                writer.Write(_ignoreInvulnerability);
                writer.Write(_amount);
                writer.Write(Cause);
                break;
            case EventType.HitResult:
                writer.Write(_position);
                writer.Write(_velocity);
                writer.Write(_color);
                writer.Write(_text);
                break;
            case EventType.SyncHealth:
                writer.Write(_targetId);
                writer.Write(_health);
                break;
            case EventType.Damage:
                writer.Write(_targetId);
                writer.Write(_health);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _type = reader.ReadEnum<EventType>();
        switch (_type)
        {
            case EventType.RequestInjure:
                _targetId = reader.ReadUInt16();
                _attackerId = reader.ReadUInt16();
                _health = reader.ReadSingle();
                _ignoreInvulnerability = reader.ReadBoolean();
                _amount = reader.ReadSingle();
                Cause = reader.ReadString();
                _requestInjureType = reader.ReadEnum<RequestInjureType>();
                break;
            case EventType.Injure:
                _targetId = reader.ReadUInt16();
                _attackerId = reader.ReadUInt16();
                _health = reader.ReadSingle();
                _ignoreInvulnerability = reader.ReadBoolean();
                _amount = reader.ReadSingle();
                Cause = reader.ReadString();
                break;
            case EventType.HitResult:
                _position = reader.ReadVector3();
                _velocity = reader.ReadVector3();
                _color = reader.ReadColor();
                _text = reader.ReadString();
                break;
            case EventType.SyncHealth:
                _targetId = reader.ReadUInt16();
                _health = reader.ReadSingle();
                break;
            case EventType.Damage:
                _targetId = reader.ReadUInt16();
                _health = reader.ReadSingle();
                break;
        }
    }

    public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
    {
        switch (_type)
        {
            case EventType.RequestInjure:
                projectNet.FindEntityById(_targetId, entity =>
                {
                    var health = entity.FindComponent<ComponentHealth>();
                    if (health == null)
                    {
                        return;
                    }

                    if (_attackerId == 0)
                    {
                        health.Injure(_amount, null, _ignoreInvulnerability, Cause);
                    }
                    else
                    {
                        projectNet.FindEntityById(_attackerId, entity2 =>
                        {
                            var attacker = entity2.FindComponent<ComponentCreature>();
                            health.Injure(_amount, attacker, _ignoreInvulnerability, Cause);
                        });
                    }
                });

                break;
            case EventType.Injure:
                projectNet.FindEntityById(_targetId, entity =>
                {
                    var health = entity.FindComponent<ComponentHealth>();
                    ComponentCreature? attacker;
                    if (health == null)
                    {
                        return;
                    }

                    if (_attackerId == 0)
                    {
                        health.NetInjure(_amount, null, Cause);
                        health.Health = _health;
                    }
                    else
                    {
                        projectNet.FindEntityById(_attackerId, entity2 =>
                        {
                            attacker = entity2.FindComponent<ComponentCreature>();
                            health.NetInjure(_amount, attacker, Cause);
                            health.Health = _health;
                        });
                    }
                });
                break;
            case EventType.HitResult:
                var particleSystem = new HitValueParticleSystem(_position, _velocity, _color, _text);
                var pitch = new Random().Float(-0.2f, 0.2f);
                projectNet.FindSubsystem<SubsystemParticles>(true)!.AddParticleSystem(particleSystem);
                projectNet.FindSubsystem<SubsystemAudio>(true)!.PlaySound("Audio/Swoosh", 1f, pitch, _position, 3f, false);
                break;
            case EventType.SyncHealth:
                projectNet.FindEntityById(_targetId, e =>
                {
                    var h = e.FindComponent<ComponentHealth>();
                    h?.Health = _health;
                });
                break;
            case EventType.Damage:
                projectNet.FindEntityById(_targetId, e =>
                {
                    var h = e.FindComponent<ComponentDamage>();
                    h?.HitPoints = _health;
                });
                break;
        }
    }
}
