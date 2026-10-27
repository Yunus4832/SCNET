using Engine.Core.Mathematics;

using Game.Protocol.Blocks;
using Game.Protocol.Entities;
using Game.Protocol.Serialization;
using Game.Protocol.World;

namespace Game.Protocol.Test;

public sealed class EntityEventCodecTest
{
    public static IEnumerable<object[]> Messages()
    {
        yield return
        [
            new EntityFirePackage { EntityId = 42, AttackerEntityId = 43, Duration = 5 }
        ];
        yield return [new BlockFireAddedPackage { X = -2, Y = 64, Z = 3, Duration = 0.5f }];
        yield return [new BlockFireRemovedPackage { X = -2, Y = 64, Z = 3 }];
        foreach (var type in Enum.GetValues<ComponentBehaviorPackage.EventType>())
        {
            yield return
            [
                type switch
                {
                    ComponentBehaviorPackage.EventType.ChaseBehavior => (ComponentBehaviorPackage)
                        new CreatureChaseStatePackage { EntityId = 42, IsActive = true },
                    ComponentBehaviorPackage.EventType.RandomPeck => new
                        CreaturePeckStatePackage { EntityId = 42, IsActive = true },
                    ComponentBehaviorPackage.EventType.RandomFeed => new
                        CreatureFeedStatePackage
                        { EntityId = 42, IsActive = true },
                    ComponentBehaviorPackage.EventType.EatPickable => new
                        CreatureEatStatePackage { EntityId = 42, IsActive = true },
                    ComponentBehaviorPackage.EventType.HumanRow => new
                        CreatureRowingStatePackage
                        { EntityId = 42, RowLeft = true, RowRight = false },
                    ComponentBehaviorPackage.EventType.CreatureSound => new
                        CreatureSoundPackage
                        { EntityId = 42, SkipIfRecentlyPlayed = true, SoundKind = 3 },
                    ComponentBehaviorPackage.EventType.DigInMud => new
                        CreatureDigStatePackage
                        { EntityId = 42, IsActive = true },
                    ComponentBehaviorPackage.EventType.FishOutOfWater => new
                        CreatureOutOfWaterStatePackage { EntityId = 42, IsActive = true },
                    _ => throw new InvalidOperationException("Unsupported message operation.")
                }
            ];
        }

        foreach (var type in Enum.GetValues<ComponentHealthPackage.EventType>())
        {
            yield return
            [
                type switch
                {
                    ComponentHealthPackage.EventType.HitResult => (ComponentHealthPackage)
                        new CharacterHitEffectPackage
                        {
                            Position = Vector3.One, Velocity = Vector3.UnitY, Color = Color.White, Text = "7"
                        },
                    ComponentHealthPackage.EventType.Injure => new EntityInjuryPackage
                    {
                        TargetId = 42, AttackerId = 43, Health = 0.5f, Amount = 0.25f, Cause = "test",
                        IgnoreInvulnerability = true
                    },
                    ComponentHealthPackage.EventType.RequestInjure => new
                        EntityInjuryRequestPackage
                        {
                            TargetId = 42, AttackerId = 43, Health = 0.5f, Amount = 0.25f, Cause = "test",
                            IgnoreInvulnerability = true,
                            InjureRequestType = ComponentHealthPackage.RequestInjureType.Fall
                        },
                    ComponentHealthPackage.EventType.SyncHealth => new EntityHealthPackage
                        { TargetId = 42, Health = 0.5f, Cause = "test" },
                    ComponentHealthPackage.EventType.Damage => new EntityDamagePackage
                        { TargetId = 42, Health = 0.5f },
                    _ => throw new InvalidOperationException("Unsupported message operation.")
                }
            ];
        }

        foreach (var type in Enum.GetValues<MovingBlockPackage.EventType>())
        {
            yield return
            [
                type switch
                {
                    MovingBlockPackage.EventType.Add => (MovingBlockPackage)
                        new MovingBlockAddedPackage
                            { NetworkId = 42, StateTick = 7, AddData = [1, 2, 3, 4] },
                    MovingBlockPackage.EventType.Remove => new MovingBlockRemovedPackage
                        { NetworkId = 42, StateTick = 7 },
                    MovingBlockPackage.EventType.Stopped => new MovingBlockStoppedPackage
                    {
                        NetworkId = 42, StateTick = 7, Position = Vector3.One, Velocity = Vector3.UnitX,
                        Speed = 0.5f
                    },
                    MovingBlockPackage.EventType.Update => new MovingBlockStatePackage
                    {
                        NetworkId = 42, StateTick = 7, Position = Vector3.One, Velocity = Vector3.UnitX,
                        Speed = 0.5f
                    },
                    MovingBlockPackage.EventType.PistonSound => new PistonSoundPackage
                        { NetworkId = 42, StateTick = 7, Position = Vector3.One },
                    _ => throw new InvalidOperationException("Unsupported message operation.")
                }
            ];
        }

        yield return
            [new ExplosionSoundPackage { Position = Vector3.One, Level = 2, Delay = 0.5f }];
        yield return
        [
            new ExplosionCellsPackage
            {
                Cells = new Dictionary<Point2, IReadOnlyList<ExplosionCell>>
                    { [new Point2(-1, 2)] = [new ExplosionCell(new Point3(-2, 64, 3), 0.5f)] }
            }
        ];
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void EntityEventsRoundTripEveryModeWithoutRuntimeDependencies(IProtocolMessage message)
    {
        Assert.True(ProtocolMessageCodec.IsDefined(message.GetType()));
        var writer = ProtocolMessageCodec.Encode(message);
        var bytes = writer;
        var reader = bytes;
        var restored = ProtocolMessageCodec.Decode(reader);
        Assert.Equal(message.GetType(), restored.GetType());

        var repeated = ProtocolMessageCodec.Encode(restored);
        Assert.Equal(bytes, repeated);
    }

    [Fact]
    public void ExplosionChunkCountMustFitTheFrame()
    {
        var malformed = WireTestData.ReplaceField(
            new ExplosionCellsPackage(),
            0,
            [0xdf, 0x7f, 0xff, 0xff, 0xff]
        );
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(malformed));
    }
}
