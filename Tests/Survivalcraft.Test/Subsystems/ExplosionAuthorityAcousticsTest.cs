using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Components;
using Game.Creatures;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Replication;
using Game.Network.Transport;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

using Random = Engine.Core.Mathematics.Random;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class ExplosionAuthorityAcousticsTest
{
    [Theory]
    [InlineData(100f, 30f, false)]
    [InlineData(100f, 30f, true)]
    [InlineData(101f, 35f, false)]
    [InlineData(101f, 35f, true)]
    [InlineData(4000f, 35f, false)]
    [InlineData(4001f, 40f, false)]
    [InlineData(20000f, 40f, false)]
    [InlineData(20001f, 50f, false)]
    [InlineData(100000f, 50f, false)]
    [InlineData(100001f, 70f, false)]
    [InlineData(1000000f, 70f, false)]
    [InlineData(1000001f, 100f, true)]
    public void RealPostprocessUsesPeakCellForGameplayNoiseAndRetainsAuthorityPitchDraw(
        float totalPressure,
        float noiseRange,
        bool playSound
    )
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        using var replica = new RuntimeTestProject(RuntimeSide.Client);
        var node = WorldNetworkBinding.For(project).RequireNode();
        var replicaNode = WorldNetworkBinding.For(replica).RequireNode();
        var observer = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid())
            { State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live };
        node.AddClient(observer);
        var connections = InProcessMessageConnection.CreatePair(node, observer, replicaNode, new Client(Guid.Empty));
        using var toClient = connections.ToClient;
        using var toAuthority = connections.ToAuthority;
        var peak = new Point3(450, 64, -600);
        var origin = new Vector3(peak) + new Vector3(.5f);
        var interest = new SubsystemNetworkInterest();
        project.AddSubsystem(interest);
        ((SpatialInterestIndex)Field(interest, "_spatial")!).SetLocation(observer, origin.XZ, 128f);
        var terrain = new SubsystemTerrain { Terrain = new Terrain() };
        project.AddSubsystem(terrain);
        var bodies = new SubsystemBodies();
        project.AddSubsystem(bodies);
        var noise = new SubsystemNoise();
        project.AddSubsystem(noise);
        noise.Load(new ValuesDictionary());
        project.AddSubsystem(new SubsystemPickables());
        project.AddSubsystem(new SubsystemProjectiles());
        project.AddSubsystem(new SubsystemBlockBehaviors());
        project.AddSubsystem(new SubsystemFireBlockBehavior());
        var explosions = new SubsystemExplosions();
        project.AddSubsystem(explosions);
        explosions.Load(new ValuesDictionary());
        var inside = AddListener(project, bodies, origin + new Vector3(noiseRange - .25f, 0, 0));
        var outside = AddListener(project, bodies, origin + new Vector3(noiseRange + .25f, 0, 0));
        var atWorldOrigin = AddListener(project, bodies, Vector3.Zero);
        var pressures = new SubsystemExplosions.SparseSpatialArray<float>(peak.X, peak.Y, peak.Z, 0f);
        pressures.Set(peak.X, peak.Y, peak.Z, totalPressure * .75f);
        pressures.Set(peak.X + 2, peak.Y, peak.Z, totalPressure * .25f);
        SetField(explosions, "_pressureByPoint", pressures);
        var surroundingField = typeof(SubsystemExplosions).GetField(
            "_surroundingPressureByPoint",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        surroundingField.SetValue(
            explosions,
            Activator.CreateInstance(
                surroundingField.FieldType,
                peak.X,
                peak.Y,
                peak.Z,
                Activator.CreateInstance(surroundingField.FieldType.GetGenericArguments()[0])
            )
        );
        Assert.Null(project.FindSubsystem<SubsystemAudio>());
        Assert.Null(project.FindSubsystem<SubsystemParticles>());
        Assert.Null(project.FindSubsystem<SubsystemExplosionPresentation>());
        var savedRandom = SubsystemExplosions.SharedRandom.State;
        try
        {
            const int seed = 2048;
            SubsystemExplosions.SharedRandom.Seed(seed);
            var expectedRandom = new Random(seed);
            foreach (var _ in pressures.ToDictionary()) { expectedRandom.Float(.2f, 1f); }

            foreach (var _ in bodies.Bodies)
            {
                expectedRandom.Float(.5f, 1.5f);
                expectedRandom.Float(.5f, 1.5f);
            }

            var variation = totalPressure > 1000000f ? .1f : .2f;
            var expectedPitch = playSound ? expectedRandom.Float(-variation, variation) : 0f;
            explosions.PostprocessExplosions(playSound);

            var heard = Assert.Single(inside.Heard);
            Assert.Null(heard.Source);
            Assert.Equal(origin, heard.Position);
            Assert.Equal(1f, heard.Loudness);
            Assert.Empty(outside.Heard);
            Assert.Empty(atWorldOrigin.Heard);
            Assert.Equal(expectedRandom.State, SubsystemExplosions.SharedRandom.State);
            var pending = (PendingLogicalMessages)Field(node, "_pendingPackages")!;
            if (playSound)
            {
                var outbound = Assert.Single(pending);
                var sound = Assert.IsType<ExplosionSoundPackage>(outbound.Package);
                Assert.Equal(origin, sound.Position);
                Assert.Equal(totalPressure, sound.Level);
                Assert.Equal(expectedPitch, sound.Pitch);
                Assert.Same(observer, outbound.Audience.SingleRecipient);
            }
            else { Assert.Empty(pending); }
        }
        finally { SubsystemExplosions.SharedRandom.State = savedRandom; }
    }

    [Fact]
    public void ClientPlaysTheAuthorityPitchWithoutDrawingFromTheRuleRandomStream()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var sounds = new SubsystemSpatialSoundRequests();
        project.AddSubsystem(sounds);
        project.AddSubsystem(new SubsystemParticles());
        var presentation = new SubsystemExplosionPresentation();
        project.AddSubsystem(presentation);
        presentation.Load(new ValuesDictionary());
        var requests = new List<SpatialSoundRequest>();
        sounds.Requested += requests.Add;
        var packet = new ExplosionSoundPackage
            { Position = new Vector3(450.5f, 64.5f, -599.5f), Level = 101f, Pitch = .137f };
        var decoded = ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(packet));
        var before = SubsystemExplosions.SharedRandom.State;

        new ExplosionsPackageHandler().Handle(
            Assert.IsType<ExplosionSoundPackage>(decoded),
            new PackageReceiveContext(null, WorldRuntimeContext.For(project), null)
        );

        Assert.Equal(before, SubsystemExplosions.SharedRandom.State);
        Assert.Equal(packet.Pitch, Assert.Single(requests).Pitch);
    }

    private static NoiseListener AddListener(RuntimeTestProject project, SubsystemBodies bodies, Vector3 position)
    {
        var body = new ComponentBody { Position = position, Mass = 60f, BoxSize = new Vector3(1f) };
        project.InitializeComponent(body);
        var listener = new NoiseListener();
        listener.Initialize(body.Entity, body.ValuesDictionary);
        var components = (List<Component>)Field(body.Entity, "_components")!;
        components.Add(body);
        components.Add(listener);
        bodies.OnEntityAdded(body.Entity);
        return listener;
    }

    private static object? Field(object owner, string name) =>
        owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner);

    private static void SetField(object owner, string name, object value)
    {
        owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, value);
    }

    private sealed class NoiseListener : Component, INoiseListener
    {
        public List<(ComponentBody? Source, Vector3 Position, float Loudness)> Heard { get; } = [];

        public void HearNoise(ComponentBody? sourceBody, Vector3 sourcePosition, float loudness)
        {
            Heard.Add((sourceBody, sourcePosition, loudness));
        }
    }
}
