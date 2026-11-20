using System.Reflection;

using Engine.Core.Mathematics;
using Engine.Core.Runtime;
using Engine.Graphics;

using Game.Blocks;
using Game.ClientContent;
using Game.Managers;
using Game.Modding.Blocks;
using Game.Network;
using Game.ParticleSystems;
using Game.Subsystems;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Content;

[Collection(ConfigFileCollection.Name)]
public sealed class BlockDigOutcomeTest
{
    [Fact]
    public void AClientShakeCreatesEightRealLeafParticlesAndNoOrdinaryDebris()
    {
        Dispatcher.Initialize();
        ContentManager.Initialize();
        var node = new NetNode(
            NetNodeOptions.Default,
            clientPresentation: true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        using var scope = new WorldCatalogScope(node);
        Assert.True(WorldRuntimeContext.For(scope.Project).HasPresentation);
        using var content = new ClientContentRuntime([new BuiltInClientContentModule()]);
        content.InstallResources(scope.Runtime.Content.RegisteredResources);
        _ = ContentManager.Get<Texture2D>("Textures/LeafParticle");
        using var terrain = new Terrain();
        var subsystem = new SubsystemTerrain { Terrain = terrain };
        scope.Project.InitializeSubsystem(subsystem);
        var particles = new SubsystemParticles();
        scope.Project.AddSubsystem(particles);
        var owner = new SubsystemBlockPresentation();
        scope.Project.AddSubsystem(owner);
        owner.Bind(new BlockPresentationCatalog(scope.Catalog, BuiltInBlockPresentationRegistry.Registrations));
        var block = scope.Catalog.GetBlock(ResourceId.Parse("game:oak_leaves"));
        var oldValue = AutumnLeaves(block.BlockIndex);
        var dig = block.GetDigValue(subsystem, oldValue, 0, Hit(oldValue));
        var presentation = owner.Catalog.Get(block.BlockIndex);

        presentation.OnDug(subsystem, particles, Outcome(oldValue, dig));
        var systems = (Dictionary<ParticleSystemBase, bool>)typeof(SubsystemParticles)
            .GetField("_particleSystems", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(particles)!;
        var leaves = Assert.IsType<LeavesParticleSystem>(Assert.Single(systems.Keys));
        var emitted = (LeavesParticleSystem.Particle[])typeof(ParticleSystem<LeavesParticleSystem.Particle>)
            .GetProperty("Particles", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(leaves)!;
        Assert.Equal(8, emitted.Length);
        Assert.All(emitted, particle => Assert.True(particle.IsActive));
        presentation.DestructionDebrisScale = 0f;
        presentation.OnDug(null!, null!, Outcome(oldValue, dig));
        owner.Catalog.Get(0).DestructionDebrisScale = 0f;
        owner.Catalog.Get(0).OnDug(
            null!,
            null!,
            new BlockDigOutcome(0, 0, default, default, BlockDigOutcomeKind.BlockChanged, true)
        );
        Assert.Single(systems);
    }

    private static int AutumnLeaves(int index)
    {
        var data = DeciduousLeavesBlock.SetTimeOfSeason(0, 0.8f);
        return Terrain.MakeBlockValue(index, DeciduousLeavesBlock.SetSeason(data, Season.Autumn));
    }

    private static TerrainRaycastResult Hit(int value) => new() { Value = value, CellFace = new CellFace(1, 5, 1, 4) };

    private static BlockDigOutcome Outcome(int oldValue, BlockDigData dig) =>
        new(oldValue, dig.Value, dig.CellFace, new Vector3(1.5f, 5.5f, 1.5f), dig.Kind, false);
}
