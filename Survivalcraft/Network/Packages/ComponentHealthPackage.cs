using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ComponentHealthPackage : IPackage
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

    public float Amount;

    public int AttackerId;

    public Color Color;

    public string Cause = string.Empty;

    public float Health;

    public bool IgnoreInvulnerability;

    public Vector3 Position;

    public RequestInjureType InjureRequestType;

    public int TargetId;

    public string Text = string.Empty;

    public EventType Type;

    public Vector3 Velocity;

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
        TargetId = health.Entity.EntityId;
        Health = health.Health;
        Type = EventType.SyncHealth;
    }

    public ComponentHealthPackage(ComponentDamage health)
    {
        TargetId = health.Entity.EntityId;
        Health = health.HitPoints;
        Type = EventType.Damage;
    }

    public ComponentHealthPackage(
        ComponentHealth target,
        ComponentCreature? attacker,
        float amount,
        string cause,
        bool ignoreInvulnerability = false,
        bool isRequest = false,
        RequestInjureType requestInjureType = RequestInjureType.Unknown
    )
    {
        Type = isRequest ? EventType.RequestInjure : EventType.Injure;
        Amount = amount;
        AttackerId = attacker == null ? 0 : attacker.Entity.EntityId;
        Health = target.Health;
        TargetId = target.Entity.EntityId;
        Cause = cause;
        IgnoreInvulnerability = ignoreInvulnerability;
        InjureRequestType = requestInjureType;
    }

    public ComponentHealthPackage(Vector3 position, Vector3 velocity, Color color, string text)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Text = text;
        Type = EventType.HitResult;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(Type);
        switch (Type)
        {
            case EventType.RequestInjure:
                writer.Write(TargetId);
                writer.Write(AttackerId);
                writer.Write(Health);
                writer.Write(IgnoreInvulnerability);
                writer.Write(Amount);
                writer.Write(Cause);
                writer.WriteEnum(InjureRequestType);
                break;
            case EventType.Injure:
                writer.Write(TargetId);
                writer.Write(AttackerId);
                writer.Write(Health);
                writer.Write(IgnoreInvulnerability);
                writer.Write(Amount);
                writer.Write(Cause);
                break;
            case EventType.HitResult:
                writer.Write(Position);
                writer.Write(Velocity);
                writer.Write(Color);
                writer.Write(Text);
                break;
            case EventType.SyncHealth:
                writer.Write(TargetId);
                writer.Write(Health);
                break;
            case EventType.Damage:
                writer.Write(TargetId);
                writer.Write(Health);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        Type = reader.ReadEnum<EventType>();
        switch (Type)
        {
            case EventType.RequestInjure:
                TargetId = reader.ReadInt32();
                AttackerId = reader.ReadInt32();
                Health = reader.ReadSingle();
                IgnoreInvulnerability = reader.ReadBoolean();
                Amount = reader.ReadSingle();
                Cause = reader.ReadString();
                InjureRequestType = reader.ReadEnum<RequestInjureType>();
                break;
            case EventType.Injure:
                TargetId = reader.ReadInt32();
                AttackerId = reader.ReadInt32();
                Health = reader.ReadSingle();
                IgnoreInvulnerability = reader.ReadBoolean();
                Amount = reader.ReadSingle();
                Cause = reader.ReadString();
                break;
            case EventType.HitResult:
                Position = reader.ReadVector3();
                Velocity = reader.ReadVector3();
                Color = reader.ReadColor();
                Text = reader.ReadString();
                break;
            case EventType.SyncHealth:
                TargetId = reader.ReadInt32();
                Health = reader.ReadSingle();
                break;
            case EventType.Damage:
                TargetId = reader.ReadInt32();
                Health = reader.ReadSingle();
                break;
        }
    }


}
