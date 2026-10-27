using Engine.Core.Mathematics;

using Game.Network.Transport;
using Game.Subsystems;

namespace Survivalcraft.Test.Network;

public sealed class MovingBlockPackageTest
{
    [Fact]
    public void ReentryBaselinePreservesOriginalMotionStartAndCurrentVelocity()
    {
        var set = new SubsystemMovingBlocks.MovingBlockSet
        {
            Tag = "baseline",
            NetworkId = 42,
            StartPosition = new Vector3(-10, 20, 30),
            Position = new Vector3(-5, 20, 30),
            TargetPosition = new Vector3(10, 20, 30),
            CurrentVelocity = new Vector3(2, 0, 0),
            Speed = 3,
            Smoothness = new Vector2(4, 5)
        };
        var package = MovingBlockPackageBuilder.Capture(set);
        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = Assert.IsType<MovingBlockAddedPackage>(ProtocolMessageCodec.Decode(reader));
        var subsystem = new SubsystemMovingBlocks();

        var baseline = new EntitySystem.TemplatesDatabase.ValuesDictionary();
        baseline.ApplyOverridesUseMessagePack(clone.AddData!);
        var restored = Assert.IsType<SubsystemMovingBlocks.MovingBlockSet>(subsystem.LoadAndAddMovingItem(baseline));

        Assert.Equal(set.NetworkId, restored.NetworkId);
        Assert.Equal(set.StartPosition, restored.StartPosition);
        Assert.Equal(set.Position, restored.Position);
        Assert.Equal(set.TargetPosition, restored.TargetPosition);
        Assert.Equal(set.CurrentVelocity, restored.CurrentVelocity);
        Assert.Equal(set.Speed, restored.Speed);
        Assert.Equal(set.Smoothness, restored.Smoothness);
    }

    [Fact]
    public void PistonSoundIsPositionedTransientEffectWithoutEntityBaseline()
    {
        var package = new PistonSoundPackage { Position = new Vector3(1, 2, 3) };
        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = Assert.IsType<PistonSoundPackage>(ProtocolMessageCodec.Decode(reader));

        Assert.Equal(package.Type, clone.Type);
        Assert.Equal(package.Position, clone.Position);

        Assert.Equal(PackageTransportPolicy.Effect, PackageTransportPolicy.Get(clone));
    }

    [Theory]
    [InlineData(MovingBlockPackage.EventType.Update)]
    [InlineData(MovingBlockPackage.EventType.Stopped)]
    public void MotionAndStopCarryAuthoritativePosition(MovingBlockPackage.EventType type)
    {
        var set = new SubsystemMovingBlocks.MovingBlockSet
        {
            Tag = new object(),
            NetworkId = 42,
            Position = new Vector3(1, 2, 3),
            CurrentVelocity = new Vector3(4, 5, 6),
            Speed = 7
        };
        MovingBlockPackage package = type == MovingBlockPackage.EventType.Update
            ? MovingBlockPackageBuilder.CaptureState(set) with { StateTick = 10 }
            : new MovingBlockStoppedPackage
            {
                NetworkId = set.NetworkId, StateTick = 10, Position = set.Position, Velocity = set.CurrentVelocity,
                Speed = set.Speed
            };
        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = ProtocolMessageCodec.Decode(writer);
        Assert.Equal(package.GetType(), clone.GetType());
        Assert.Equal(writer, ProtocolMessageCodec.Encode(clone));

        Assert.Equal(
            type == MovingBlockPackage.EventType.Update
                ? PackageTransportPolicy.StateStream
                : PackageTransportPolicy.ReliableEvent,
            PackageTransportPolicy.Get(clone)
        );
    }
}
