using Engine.Core;

namespace Game.Protocol.Entities;

public sealed record ComponentHealthPackage : IProtocolMessage
{
    public enum EventType
    {
        HitResult = 0,

        Injure = 1,

        RequestInjure = 2,

        SyncHealth = 3,

        Damage = 4 //船被破坏
    }

    public enum RequestInjureType
    {
        Unknown = 0, // 未知

        Choke = 1, // 自杀

        Cactus = 2, // 仙人掌刺伤

        Fall = 3, // 摔伤

        Fire = 4 // 着火
    }

    public float Amount { get; init; }

    public int AttackerId { get; init; }

    public Color Color { get; init; }

    public string Cause { get; init; } = string.Empty;

    public float Health { get; init; }

    public bool IgnoreInvulnerability { get; init; }

    public Vector3 Position { get; init; }

    public RequestInjureType InjureRequestType { get; init; }

    public int TargetId { get; init; }

    public string Text { get; init; } = string.Empty;

    public EventType Type { get; init; }

    public Vector3 Velocity { get; init; }

    public byte ID => (byte)PackageType.ComponentHealth;

    public ComponentHealthPackage()
    {
    }

    public ComponentHealthPackage(Vector3 position, Vector3 velocity, Color color, string text)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Text = text;
        Type = EventType.HitResult;
    }
}
