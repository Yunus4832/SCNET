using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Blocks;
using Game.Modding.Blocks;
using Game.ParticleSystems;
using Game.Subsystems;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class BlockFlamePresentationTest
{
    [Fact]
    public void BothAddedOverloadsGenerationModificationAndExtinguishOwnOneRealRegistration()
    {
        using var fixture = new Fixture();
        var lit = Terrain.MakeBlockValue(BuiltInBlockRegistry.GetIndex<LitFurnaceBlock>());
        var unlit = Terrain.MakeBlockValue(BuiltInBlockRegistry.GetIndex<FurnaceBlock>());
        fixture.View.OnBlockAdded(lit, 0, 1, 65, 2, null!);
        var first = Assert.Single(fixture.Created).System;
        Assert.True(fixture.Particles.ContainsParticleSystem(first));
        fixture.View.OnBlockAdded(lit, 0, 1, 65, 2);
        fixture.View.OnBlockGenerated(lit, 1, 65, 2, true);
        Assert.Single(fixture.Created);
        fixture.View.OnBlockModified(unlit, lit, 1, 65, 2);
        Assert.False(fixture.Particles.ContainsParticleSystem(first));
        fixture.View.OnBlockRemoved(lit, unlit, 1, 65, 2);
        fixture.View.OnBlockRemoved(lit, unlit, 1, 65, 2);
    }

    [Fact]
    public void TorchOrientationReplacementAndChunkDiscardReleaseRegisteredParticles()
    {
        using var fixture = new Fixture();
        var torch = BuiltInBlockRegistry.GetIndex<TorchBlock>();
        var first = Terrain.MakeBlockValue(torch, 0);
        var rotated = Terrain.MakeBlockValue(torch, 1);
        fixture.View.OnBlockGenerated(first, 1, 65, 2, false);
        fixture.View.OnBlockModified(rotated, first, 1, 65, 2);
        Assert.Equal(2, fixture.Created.Count);
        Assert.False(fixture.Particles.ContainsParticleSystem(fixture.Created[0].System));
        Assert.NotEqual(fixture.Created[0].Position, fixture.Created[1].Position);
        var chunk = (TerrainChunk)RuntimeHelpers.GetUninitializedObject(typeof(TerrainChunk));
        chunk.Origin = new Point2(0, 0);
        fixture.View.OnChunkDiscarding(chunk);
        Assert.False(fixture.Particles.ContainsParticleSystem(fixture.Created[1].System));
        fixture.View.OnChunkDiscarding(chunk);
    }

    [Fact]
    public void TerrainClosingAndRepeatedDisposeDetachAndDoNotRecreateOrDoubleRemove()
    {
        using var fixture = new Fixture();
        var value = Terrain.MakeBlockValue(BuiltInBlockRegistry.GetIndex<WickerLampBlock>());
        fixture.View.OnBlockAdded(value, 0, 1, 65, 2);
        var system = Assert.Single(fixture.Created).System;
        fixture.Particles.RemoveParticleSystem(system);
        fixture.Terrain.Dispose();
        fixture.View.Dispose();
        fixture.View.OnBlockGenerated(value, 1, 65, 2, true);
        Assert.Single(fixture.Created);
        Assert.False(fixture.Particles.ContainsParticleSystem(system));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly RuntimeTestProject _project = new(RuntimeSide.Client);

        public SubsystemTerrain Terrain { get; } = new();

        public SubsystemParticles Particles { get; } = new();

        public SubsystemBlockFlamePresentation View { get; }

        public List<(Vector3 Position, FireParticleSystem System)> Created { get; } = [];

        public Fixture()
        {
            _project.AddSubsystem(Terrain);
            _project.AddSubsystem(Particles);
            View = new SubsystemBlockFlamePresentation((position, _, _) =>
                {
                    // Real registration/removal uses SubsystemParticles; only texture loading is omitted.
                    var system = (FireParticleSystem)RuntimeHelpers.GetUninitializedObject(typeof(FireParticleSystem));
                    Created.Add((position, system));
                    return system;
                }
            );
            _project.AddSubsystem(View);
            View.Load(new ValuesDictionary());
        }

        public void Dispose() => _project.Dispose();
    }
}
