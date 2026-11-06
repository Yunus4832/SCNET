using Engine.Core.Mathematics;

using Game.Protocol.Blocks;
using Game.Protocol.Entities;
using Game.Protocol.Localization;
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
                    ComponentBehaviorPackage.EventType.Shapeshift => new
                        CreatureShapeshiftStatePackage { EntityId = 42, StartTime = 123.5 },
                    ComponentBehaviorPackage.EventType.ClothingEffect => new ClothingEffectPackage
                        { EntityId = 42, Kind = ClothingEffectKind.ArmorImpact, ClothingValue = 203 },
                    ComponentBehaviorPackage.EventType.WhalePlume => new
                        CreatureWhalePlumePackage { EntityId = 42, IsActive = true, Size = 1, Duration = 1.2f },
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
                            Position = Vector3.One, Velocity = Vector3.UnitY, Color = Color.White,
                            Text = LocalizedText.Literal("7"),
                            PlayBodyImpact = true, BodyPosition = Vector3.UnitX
                        },
                    ComponentHealthPackage.EventType.Injure => new EntityInjuryPackage
                    {
                        TargetId = 42, AttackerId = 43, Health = 0.5f, Amount = 0.25f,
                        Cause = LocalizedText.Literal("test"),
                        IgnoreInvulnerability = true
                    },
                    ComponentHealthPackage.EventType.RequestInjure => new
                        EntityInjuryRequestPackage
                        {
                            TargetId = 42, AttackerId = 43, Health = 0.5f, Amount = 0.25f,
                            Cause = LocalizedText.Literal("test"),
                            IgnoreInvulnerability = true,
                            InjureRequestType = ComponentHealthPackage.RequestInjureType.Fall
                        },
                    ComponentHealthPackage.EventType.SyncHealth => new EntityHealthPackage
                        { TargetId = 42, Health = 0.5f, Cause = LocalizedText.Literal("test") },
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
            [new ExplosionSoundPackage { Position = Vector3.One, Level = 2, Pitch = 0.15f }];
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
    public void BodyImpactCanTravelWithoutFloatingTextAndRejectsNonFiniteCoordinates()
    {
        var effect = new CharacterHitEffectPackage { PlayBodyImpact = true, BodyPosition = Vector3.UnitX };
        Assert.Equal(effect, ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(effect)));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(
                effect with
                {
                    BodyPosition = new Vector3(float.NaN, 0, 0)
                }
            )
        );
    }

    [Fact]
    public void ClothingEffectsRoundTripAndRejectUnknownKinds()
    {
        foreach (var kind in Enum.GetValues<ClothingEffectKind>())
        {
            var effect = new ClothingEffectPackage { EntityId = 42, Kind = kind, ClothingValue = 203 };
            Assert.Equal(effect, ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(effect)));
        }

        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(
                new ClothingEffectPackage
                {
                    Kind = (ClothingEffectKind)255
                }
            )
        );
    }

    [Fact]
    public void CreatureSoundKindsStayWithinTheDeclaredPlaybackTable()
    {
        for (byte kind = 0; kind <= 10; kind++)
        {
            var sound = new CreatureSoundPackage { EntityId = 42, SoundKind = kind };
            Assert.Equal(sound, ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(sound)));
        }

        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Encode(new CreatureSoundPackage { SoundKind = 11 })
        );
    }

    [Fact]
    public void BodySnapshotCarriesCreatureActionsWithoutRenderingTypes()
    {
        var packet = new BodyStatesPackage
        {
            StateTick = 1,
            BodyList =
            [
                new SubsystemBodyPackage.BodyItem
                {
                    CreatureId = 42, Attacking = true, Feeding = true, AttackPhase = .6f,
                    AttackCounter = 3, Bend = .5f, DigIn = .2f
                }
            ]
        };
        var decoded =
            Assert.IsType<BodyStatesPackage>(ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(packet)));
        Assert.Equal(packet.BodyList[0], decoded.BodyList[0]);
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(
                packet with
                {
                    BodyList = [new SubsystemBodyPackage.BodyItem { AttackPhase = float.NaN }]
                }
            )
        );
    }

    [Fact]
    public void SurvivalAuthorityResultsRoundTripAndRejectMalformedValues()
    {
        IProtocolMessage[] messages =
        [
            new ComponentFluPackage
            {
                EntityId = 42, FluDuration = 900, EffectSequence = 2, BlackoutDuration = 3,
                CoughDuration = 4, LastWasCough = true, PackageEventType = ComponentFluPackage.EventType.FluEffect
            },
            new SleepRequestPackage { EntityId = 42 },
            new SleepResultPackage { EntityId = 42, StartTime = 123, Result = true, AllowManualWakeup = false },
            new WakeupRequestPackage { EntityId = 42, SleepStartTime = 123 },
            new WakeupPackage { EntityId = 42, Reason = SleepWakeReason.Wet },
            new VitalStatePackage { EntityId = 42, Stamina = .5f, Temperature = 12, EnvironmentTemperature = -5 },
            new VitalFeedbackPackage { EntityId = 42, MessageKey = 8, IsEating = true }
        ];
        foreach (var message in messages)
        {
            Assert.Equal(message, ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(message)));
        }

        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Encode(new VitalStatePackage { Stamina = float.NaN })
        );
        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Encode(new WakeupRequestPackage { SleepStartTime = double.NaN })
        );
        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Encode(new ComponentFluPackage { BlackoutDuration = -1 })
        );
    }

    [Fact]
    public void SicknessStateCarriesEffectsWithoutReplayingRules()
    {
        var message = new ComponentSicknessPackage
        {
            EntityId = 42, SicknessDuration = 900, EffectSequence = 3,
            PukeDuration = 2, NauseaDuration = .8f, IsNausea = true
        };
        Assert.Equal(message, ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(message)));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(message with { PukeDuration = float.NaN })
        );
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(message with { EffectSequence = -1 }));
    }

    [Fact]
    public void HealthDeathFactsAndDurabilityLifecycleRoundTrip()
    {
        IProtocolMessage[] messages =
        [
            new EntityHealthPackage
                { TargetId = 42, Health = 0, Cause = LocalizedText.Literal("Fall"), DeathTime = 123.5 },
            new EntityInjuryPackage
                { TargetId = 42, Health = 0, Cause = LocalizedText.Literal("Fall"), DeathTime = 123.5, Amount = 1 },
            new EntityDamagePackage { TargetId = 42, Health = 0, Destroyed = true },
            new EntityDamagePackage { TargetId = 42, Health = .5f, IsAttack = true },
            new EntityFirePackage { EntityId = 42, Duration = 0 }
        ];
        foreach (var message in messages)
        {
            Assert.Equal(message, ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(message)));
        }
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidDeathTimesAreRejected(double deathTime)
    {
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(
                new EntityHealthPackage { TargetId = 42, DeathTime = deathTime }
            )
        );
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(
                new EntityInjuryPackage { TargetId = 42, DeathTime = deathTime }
            )
        );
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidEntityFireDurationsAreRejected(float duration)
    {
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(
                new EntityFirePackage { EntityId = 42, Duration = duration }
            )
        );
    }

    [Fact]
    public void WhalePlumeStopRoundTripsAndInvalidDurationsAreRejected()
    {
        var stop = new CreatureWhalePlumePackage { EntityId = 42 };
        Assert.Equal(stop, ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(stop)));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(
                new CreatureWhalePlumePackage { EntityId = 42, IsActive = true, Size = 1, Duration = float.NaN }
            )
        );
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
