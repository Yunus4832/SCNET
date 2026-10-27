using Engine.Core.Mathematics;

using Game.Network.Transport;
using Game.Subsystems;
using Game.World;

namespace Survivalcraft.Test.Network;

public sealed class ProjectilePackageTest
{
    [Theory]
    [InlineData(ProjectilePackage.EffectType.Impact)]
    [InlineData(ProjectilePackage.EffectType.Debris)]
    [InlineData(ProjectilePackage.EffectType.WaterSplash)]
    [InlineData(ProjectilePackage.EffectType.MagmaSplash)]
    [InlineData(ProjectilePackage.EffectType.Sizzle)]
    public void EffectDoesNotRequireAReplicatedProjectile(ProjectilePackage.EffectType effect)
    {
        var package = new ProjectileEffectPackage
            { Effect = effect, Value = 100, Position = new Vector3(1, 2, 3) };
        var clone = RoundTrip(package);

        Assert.Equal(effect, clone.Effect);
        Assert.Equal(package.Position, clone.Position);
        Assert.Equal(package.Value, clone.Value);
        Assert.Equal(0, clone.NetworkId);
        Assert.Equal(PackageTransportPolicy.Effect, PackageTransportPolicy.Get(clone));
    }

    [Fact]
    public void MotionUpdatePreservesStateAndUsesUnreliableStateStream()
    {
        var package = ProjectilePackageBuilder.CaptureState(
                new Projectile
                {
                    NetworkId = 42,
                    Position = new Vector3(1, 2, 3),
                    Rotation = new Vector3(4, 5, 6),
                    Velocity = new Vector3(7, 8, 9),
                    AngularVelocity = new Vector3(10, 11, 12)
                }
            ) with
            {
                StateTick = 5
            };
        var clone = RoundTrip(package);

        Assert.Equal(package.Position, clone.Position);
        Assert.Equal(package.Rotation, clone.Rotation);
        Assert.Equal(package.Velocity, clone.Velocity);
        Assert.Equal(package.AngularVelocity, clone.AngularVelocity);
        Assert.Equal(5u, clone.StateTick);
        Assert.Equal(PackageTransportPolicy.StateStream, PackageTransportPolicy.Get(clone));
    }

    [Fact]
    public void MotionRejectsStaleStateAndHandlesTickWraparound()
    {
        var projectile = new Projectile { LastNetworkStateTick = uint.MaxValue };
        var update = new ProjectileStatePackage
            { StateTick = 0, Position = new Vector3(1, 2, 3) };

        Assert.True(SubsystemProjectiles.ApplyNetworkMotion(projectile, update));
        Assert.Equal(update.Position, projectile.Position);
        update = update with { StateTick = uint.MaxValue, Position = Vector3.Zero };
        Assert.False(SubsystemProjectiles.ApplyNetworkMotion(projectile, update));
        Assert.Equal(new Vector3(1, 2, 3), projectile.Position);
    }

    [Fact]
    public void EnterSnapshotPreservesIdentityAndCurrentMotion()
    {
        var projectile = new Projectile
        {
            NetworkId = 42,
            Value = 100,
            Position = new Vector3(12f, 64f, 18f),
            Velocity = new Vector3(2f, 3f, 4f),
            AngularVelocity = new Vector3(5f, 6f, 7f),
            TrailOffset = new Vector3(0f, 0.5f, 0f),
            IsFireProjectile = true
        };

        var clone = RoundTrip(ProjectilePackageBuilder.Capture(projectile));

        Assert.Equal(ProjectilePackage.EventType.Add, clone.Type);
        Assert.Equal(projectile.NetworkId, clone.NetworkId);
        Assert.Equal(projectile.Value, clone.Value);
        Assert.Equal(projectile.Position, clone.Position);
        Assert.Equal(projectile.Velocity, clone.Velocity);
        Assert.Equal(projectile.AngularVelocity, clone.AngularVelocity);
        Assert.Equal(projectile.TrailOffset, clone.TrailOffset);
        Assert.True(clone.IsFireProjectile);
        Assert.Equal(PackageTransportPolicy.ReliableEvent, PackageTransportPolicy.Get(clone));
    }

    [Fact]
    public void LeaveMessageContainsOnlyTypeAndIdentity()
    {
        var writer =
            ProtocolMessageCodec.Encode(new ProjectileRemovedPackage { NetworkId = 42 });
        var reader = writer;
        var clone = Assert.IsType<ProjectileRemovedPackage>(ProtocolMessageCodec.Decode(reader));

        Assert.Equal(ProjectilePackage.EventType.Remove, clone.Type);
        Assert.Equal(42, clone.NetworkId);

        Assert.Equal(PackageTransportPolicy.ReliableEvent, PackageTransportPolicy.Get(clone));
    }

    private static T RoundTrip<T>(T package) where T : ProjectilePackage
    {
        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = Assert.IsType<T>(ProtocolMessageCodec.Decode(reader));
        return clone;
    }
}
