using System.Reflection;
using System.Runtime.CompilerServices;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Components;
using Game.Components.Behavior;
using Game.Network;
using Game.Network.Handling.Presentation;
using Game.Network.Replication;
using Game.Network.Transport;
using Game.ParticleSystems;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class CetaceanBreatheRuntimeTest
{
    [Fact]
    public void AuthorityBreathUsesSimulationTimeAndPublishesPlumeWithoutLoadingTextures()
    {
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var behavior = CreateBehavior(authority);
        var node = WorldNetworkBinding.For(authority).RequireNode();
        var clientNode = WorldNetworkBinding.For(client).RequireNode();
        var observer = new Client(null, 1, Guid.NewGuid(), Guid.Empty)
        {
            State = ClientState.Playing,
            ConnectionPhase = ConnectionPhase.Live
        };
        node.AddClient(observer);
        var server = new Client(Guid.Empty);
        var pair = InProcessMessageConnection.CreatePair(node, observer, clientNode, server);
        using var toClient = pair.ToClient;
        using var toAuthority = pair.ToAuthority;
        authority.FindSubsystem<SubsystemNetworkInterest>()!.Entities.Seed(
            observer,
            EntityInterestGroup.Creatures,
            [behavior.Entity.EntityId]
        );

        behavior.Update(0.1f);
        Assert.True(behavior.ImportanceLevel > 0);
        behavior.IsActive = true;
        behavior.EnterBreathe();
        var presentation = new ComponentCetaceanBreathePresentation();
        authority.InitializeComponent(presentation);
        presentation.Load(new ValuesDictionary(), null!);
        presentation.Update(1f);
        behavior.Update(3.9f);
        Assert.True(behavior.ImportanceLevel > 0);
        behavior.Update(0.5f);
        Assert.Equal(0, behavior.ImportanceLevel);
        behavior.IsActive = false;
        behavior.Update(0.1f);

        var messages = new List<CreatureWhalePlumePackage>();
        for (var i = 0; i < 10 && messages.Count < 2; i++)
        {
            // A cold serializer can consume one outbound time budget before the stop event is sent.
            typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(node, null);
            WorldNetworkBinding.For(clientNode.Runtime).Inbox.Drain(
                10,
                received => messages.Add(Assert.IsType<CreatureWhalePlumePackage>(received.Package))
            );
        }

        Assert.Collection(
            messages,
            start =>
            {
                Assert.True(start.IsActive);
                Assert.InRange(start.Duration, 1f, 1.3f);
            },
            stop => Assert.False(stop.IsActive)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClientReplaysPlumeWithoutRunningAuthorityBehaviorAndStopsOnRemovalOrDisposal(bool dispose)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var behavior = CreateBehavior(project);
        project.AddSubsystem(new SubsystemParticles());
        project.AddSubsystem(new SubsystemAudio());
        var presentation = new ComponentCetaceanBreathePresentation();
        presentation.Initialize(behavior.Entity, behavior.ValuesDictionary);
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(behavior.Entity)!).Add(presentation);
        presentation.Load(new ValuesDictionary(), null!);
        var runtime = WorldRuntimeContext.For(project);
        var message = new CreatureWhalePlumePackage
        {
            EntityId = behavior.Entity.EntityId, IsActive = true, Size = 1f, Duration = 1.2f
        };
        var restored = Assert.IsType<CreatureWhalePlumePackage>(
            ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(message))
        );
        new ClientComponentBehaviorPackageHandler().Handle(
            restored,
            new PackageReceiveContext(
                WorldNetworkBinding.For(runtime).RequireNode(),
                runtime,
                new Client(Guid.Empty)
            )
        );
        behavior.Update(10f);
        Assert.Equal(string.Empty, behavior.CurrentState);
        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(behavior, runtime));
        Assert.Equal(message, GetField("_pendingPlume").GetValue(presentation));

        var particle = (WhalePlumeParticleSystem)RuntimeHelpers.GetUninitializedObject(
            typeof(WhalePlumeParticleSystem)
        );
        GetField("_particleSystem").SetValue(presentation, particle);
        Assert.True(SubsystemUpdate.ShouldScheduleUpdateable(presentation, runtime));
        if (dispose) { presentation.Dispose(); }
        else { presentation.OnEntityRemoved(); }

        presentation.ApplyPlume(message);
        presentation.Update(1f);
        Assert.True(particle.IsStopped);
        Assert.Null(GetField("_pendingPlume").GetValue(presentation));
    }

    private static FieldInfo GetField(string name)
    {
        return typeof(ComponentCetaceanBreathePresentation).GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
    }

    private static TestBreatheBehavior CreateBehavior(RuntimeTestProject project)
    {
        project.AddSubsystem(new UnloadedTerrain());
        project.AddSubsystem(new SubsystemNetworkInterest());
        var behavior = new TestBreatheBehavior();
        project.InitializeComponent(behavior);
        var entity = behavior.Entity;
        entity.EntityId = 42;
        var body = new ComponentBody();
        var creature = new ComponentCreature { ComponentBody = body, ComponentHealth = new ComponentHealth() };
        var components = (List<Component>)typeof(Entity).GetField(
            "_components",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!.GetValue(entity)!;
        components.AddRange([behavior, body, creature, new ComponentPathfinding()]);
        ((Dictionary<Entity, bool>)typeof(Project).GetField(
            "entityDictionary",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!.GetValue(project)!).Add(entity, true);
        behavior.Load(new ValuesDictionary(), new IdToEntityMap(new Dictionary<int, Entity>()));
        return behavior;
    }

    private sealed class TestBreatheBehavior : ComponentCetaceanBreatheBehavior
    {
        internal string CurrentState => stateMachine.CurrentState;

        internal void EnterBreathe() => stateMachine.TransitionTo("Breathe");
    }

    private sealed class UnloadedTerrain : SubsystemTerrain
    {
        public override void Dispose()
        {
        }
    }
}
