using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.ParticleSystems;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class BucketPresentationTest
{
    [Fact]
    public void AuthorityLoadsBucketRulesWithoutParticlesOrPalettePresentation()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        project.AddSubsystem(new SubsystemTerrain());
        project.AddSubsystem(new SubsystemSpatialSoundRequests());
        var buckets = new SubsystemBucketBlockBehavior();
        project.AddSubsystem(buckets);
        buckets.Load(new ValuesDictionary());
    }

    [Fact]
    public void ClientResolvesWorldPaletteAndStripperColorAndOwnsActualRegistrations()
    {
        using var fixture = new Fixture();
        fixture.Deliver(fixture.Buckets, new PaintEffectRequest(Vector3.One, Vector3.UnitY, 2));
        Assert.Equal(fixture.Palette.GetColor(2), fixture.Created[0].Color);
        Assert.True(fixture.Particles.ContainsParticleSystem(fixture.Created[0].System));
        fixture.Deliver(fixture.Buckets, new PaintEffectRequest(Vector3.One, Vector3.UnitY, null));
        Assert.Equal(new Color(128, 128, 128, 128), fixture.Created[1].Color);
        fixture.Particles.RemoveParticleSystem(fixture.Created[0].System);
        fixture.Terrain.Dispose();
        Assert.All(fixture.Created, created => Assert.False(fixture.Particles.ContainsParticleSystem(created.System)));
        fixture.View.Dispose();
        fixture.Deliver(fixture.Buckets, new PaintEffectRequest(Vector3.One, Vector3.UnitY, 2));
        Assert.Equal(2, fixture.Created.Count);
    }

    [Fact]
    public void WrongWorldSourceAndClosedWorldCannotCreatePaint()
    {
        using var fixture = new Fixture();
        using var other = new RuntimeTestProject(RuntimeSide.Client);
        var wrongSource = new SubsystemBucketBlockBehavior();
        other.AddSubsystem(wrongSource);
        fixture.Deliver(wrongSource, new PaintEffectRequest(Vector3.One, Vector3.UnitY, 0));
        Assert.Empty(fixture.Created);
        WorldRuntimeContext.For(fixture.Project).Close();
        fixture.Deliver(fixture.Buckets, new PaintEffectRequest(Vector3.One, Vector3.UnitY, 0));
        Assert.Empty(fixture.Created);
    }

    private sealed class Fixture : IDisposable
    {
        public RuntimeTestProject Project { get; } = new(RuntimeSide.Client);

        public SubsystemBucketBlockBehavior Buckets { get; } = new();

        public SubsystemTerrain Terrain { get; } = new();

        public SubsystemPalette Palette { get; } = new();

        public SubsystemParticles Particles { get; } = new();

        public SubsystemBucketPresentation View { get; }

        public List<(Color Color, PaintParticleSystem System)> Created { get; } = [];

        public Action<SubsystemBucketBlockBehavior, PaintEffectRequest> Deliver { get; }

        public Fixture()
        {
            Project.AddSubsystem(new SubsystemGameInfo { WorldSettings = new WorldSettings() });
            Project.AddSubsystem(Buckets);
            Project.AddSubsystem(Terrain);
            Project.AddSubsystem(Palette);
            Palette.Load(new ValuesDictionary());
            Project.AddSubsystem(Particles);
            View = new SubsystemBucketPresentation((_, _, _, color) =>
                {
                    var system =
                        (PaintParticleSystem)RuntimeHelpers.GetUninitializedObject(typeof(PaintParticleSystem));
                    Created.Add((color, system));
                    return system;
                }
            );
            Project.AddSubsystem(View);
            View.Load(new ValuesDictionary());
            Deliver = (Action<SubsystemBucketBlockBehavior, PaintEffectRequest>)typeof(SubsystemBucketBlockBehavior)
                .GetField("PaintEffectRequested", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Buckets)!;
        }

        public void Dispose() => Project.Dispose();
    }
}
