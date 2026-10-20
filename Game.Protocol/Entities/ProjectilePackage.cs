using Engine.Core;

namespace Game.Protocol.Entities;

public sealed record ProjectilePackage : IProtocolMessage
{
    public enum EventType : byte
    {
        Add,
        Remove,
        Update,
        Effect
    }
    public enum EffectType : byte
    {
        Impact,
        Debris,
        WaterSplash,
        MagmaSplash,
        Sizzle
    }
    public EffectType Effect { get; init; }
    public Vector3 AngularVelocity { get; init; }
    public bool IsFireProjectile { get; init; }
    public int NetworkId { get; init; }
    public int OwnerId { get; init; }
    public Vector3 Position { get; init; }
    public Vector3 Rotation { get; init; }
    public uint StateTick { get; init; }
    public Vector3 TrailOffset { get; init; }
    public int Value { get; init; }
    public EventType Type { get; init; }
    public Vector3 Velocity { get; init; }
    public byte ID => (byte)PackageType.Projectile;
    public ProjectilePackage() { }
    public ProjectilePackage(int networkId) { NetworkId = networkId; Type = EventType.Remove; }
}
