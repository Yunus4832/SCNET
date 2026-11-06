using System.Collections;
using System.Reflection;

using Engine.Core.Mathematics;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.ParticleSystems;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

using EngineRandom = Engine.Core.Mathematics.Random;

namespace Survivalcraft.Test.Subsystems;

public sealed class SubsystemProjectilesPresentationTest
{
    [Fact]
    public void EffectSubscribersDoNotChangeThePhysicsRandomSequence()
    {
        var silent = new SubsystemProjectiles();
        var observed = new SubsystemProjectiles();
        RandomOf(silent).Seed(12345);
        RandomOf(observed).Seed(12345);
        var effects = new List<ProjectileEffect>();
        observed.EffectRequested += effects.Add;
        silent.NotifyReplicatedEffect(ProjectilePackage.EffectType.WaterSplash, 1, Vector3.One, 0f);
        observed.NotifyReplicatedEffect(ProjectilePackage.EffectType.WaterSplash, 1, Vector3.One, 0f);
        var expected = new EngineRandom(12345);
        Assert.Equal(expected.Float(-0.2f, 0.2f), Assert.Single(effects).Pitch);
        Assert.Equal(expected.State, RandomOf(silent).State);
        Assert.Equal(RandomOf(silent).State, RandomOf(observed).State);
        Assert.Equal(expected.Vector3(2f), RandomOf(observed).Vector3(2f));
    }

    [Fact]
    public void TrailReplacementIsObservableWithoutAnyParticleObjectInRules()
    {
        var rules = new SubsystemProjectiles();
        var projectile = new Projectile { NetworkId = 1 };
        var changes = 0;
        rules.TrailChanged += _ => changes++;
        var first = new ProjectileTrailDescription(ProjectileTrailKind.Smoke, Vector3.One, 20, 0.5f, 100d, Color.White);
        rules.SetTrail(projectile, first);
        rules.SetTrail(projectile, first with { });
        rules.SetTrail(projectile, first with { Size = 0.75f });
        Assert.Equal(2, changes);
        Assert.Equal(0.75f, projectile.Trail!.Size);
        rules.SetTrail(projectile, null);
        Assert.Equal(3, changes);
        Assert.Null(projectile.Trail);
        Assert.Null(typeof(Projectile).GetField("TrailParticleSystem"));
    }

    [Fact]
    public void FiredTrailInferenceAndExplicitSmokeUseTheExistingWireFields()
    {
        var rules = new SubsystemProjectiles();
        var projectile = new Projectile { NetworkId = 1, IsFireProjectile = true };
        var inferred = new ProjectileTrailDescription(
            ProjectileTrailKind.Smoke,
            Vector3.One,
            20,
            0.5f,
            100d,
            Color.White
        );
        rules.SetTrail(projectile, inferred);
        var captured = ProjectilePackageBuilder.Capture(projectile);
        Assert.Equal(inferred.Offset, captured.TrailOffset);
        Assert.Null(captured.SmokeTrail);
        rules.SetTrail(projectile, inferred with { PublishSmokeSnapshot = true });
        captured = ProjectilePackageBuilder.Capture(projectile);
        Assert.Equal(inferred.EndTime, captured.SmokeTrail!.EndTime);
        Assert.Equal(inferred.Size, captured.SmokeTrail.Size);
        Assert.Equal(inferred.Color, captured.SmokeTrail.Color);
    }

    [Fact]
    public void EffectPackagesRemainServerToClientAndDoNotRequireRenderingServices()
    {
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        var clientRules = new SubsystemProjectiles();
        var authorityRules = new SubsystemProjectiles();
        client.AddSubsystem(clientRules);
        authority.AddSubsystem(authorityRules);
        var effects = new List<ProjectileEffect>();
        clientRules.EffectRequested += effects.Add;
        authorityRules.EffectRequested +=
            _ => throw new InvalidOperationException("Client cannot inject an authority effect.");
        var package = new ProjectileEffectPackage
            { Effect = ProjectilePackage.EffectType.Impact, Value = 3, Position = Vector3.One };
        var handler = new ProjectilePackageHandler();
        var clientRuntime = WorldRuntimeContext.For(client);
        handler.Handle(
            package,
            new PackageReceiveContext(WorldNetworkBinding.For(clientRuntime).RequireNode(), clientRuntime, null)
        );
        var authorityRuntime = WorldRuntimeContext.For(authority);
        handler.Handle(
            package,
            new PackageReceiveContext(WorldNetworkBinding.For(authorityRuntime).RequireNode(), authorityRuntime, null)
        );
        Assert.Equal(new ProjectileEffect(package.Effect, package.Value, package.Position, 0f), Assert.Single(effects));
    }

    [Fact]
    public void ClientTrailStoppingDoesNotRewriteProjectileStateAndDisposeUnsubscribes()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var rules = new SubsystemProjectiles();
        var presentation = new SubsystemProjectilesPresentation();
        project.InitializeSubsystem(presentation);
        var description = new ProjectileTrailDescription(
            ProjectileTrailKind.Smoke,
            Vector3.UnitX,
            20,
            0.5f,
            100d,
            Color.White
        );
        var projectile = new Projectile
            { NetworkId = 7, Position = Vector3.One, Velocity = new Vector3(1, 2, 3), NoChunk = true };
        rules.SetTrail(projectile, description);
        ((List<Projectile>)Field(typeof(SubsystemProjectiles), "_projectiles").GetValue(rules)!).Add(projectile);
        Field(typeof(SubsystemProjectilesPresentation), "_projectiles").SetValue(presentation, rules);
        Field(typeof(SubsystemProjectilesPresentation), "_subsystemGameInfo")
            .SetValue(presentation, new SubsystemGameInfo { TotalElapsedGameTime = 10d });
        Field(typeof(SubsystemProjectilesPresentation), "_subsystemParticles")
            .SetValue(presentation, new SubsystemParticles());
        typeof(SubsystemProjectilesPresentation).GetMethod("Subscribe", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(presentation, null);
        var trail = new TestTrail();
        var stateType = typeof(SubsystemProjectilesPresentation).GetNestedType("TrailState", BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(stateType, description, trail)!;
        var states = (IDictionary)Field(typeof(SubsystemProjectilesPresentation), "_trails").GetValue(presentation)!;
        states.Add(projectile, state);
        var position = projectile.Position;
        var velocity = projectile.Velocity;
        var randomState = RandomOf(rules).State;
        presentation.Update(0.016f);
        Assert.True(trail.IsStopped);
        Assert.Equal(position, projectile.Position);
        Assert.Equal(velocity, projectile.Velocity);
        Assert.Same(description, projectile.Trail);
        Assert.Equal(randomState, RandomOf(rules).State);
        rules.SetTrail(projectile, null);
        Assert.Empty(states);
        presentation.Dispose();
        presentation.Dispose();
        presentation.Draw(null!, 10);
        Assert.Null(Field(typeof(SubsystemProjectiles), "TrailChanged").GetValue(rules));
        Assert.Null(Field(typeof(SubsystemProjectiles), "ProjectileAdded").GetValue(rules));
        Assert.Null(Field(typeof(SubsystemProjectiles), "ProjectileRemoved").GetValue(rules));
        Assert.Null(Field(typeof(SubsystemProjectiles), "EffectRequested").GetValue(rules));
    }

    private static FieldInfo Field(Type type, string name) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static EngineRandom RandomOf(SubsystemProjectiles rules) =>
        (EngineRandom)Field(typeof(SubsystemProjectiles), "_random").GetValue(rules)!;

    private sealed class TestTrail : ParticleSystemBase, ITrailParticleSystem
    {
        public Vector3 Position { get; set; }

        public bool IsStopped { get; set; }

        public override void Draw(Game.Cameras.Camera camera)
        {
        }

        public override bool Simulate(float dt) => false;
    }
}
