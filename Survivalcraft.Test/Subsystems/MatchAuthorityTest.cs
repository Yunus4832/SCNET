using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Blocks;
using Game.Components;
using Game.Modding.Blocks;
using Game.Network;
using Game.Network.Handling;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Execution;
using Game.Terrains.Lighting;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class MatchAuthorityTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplicaMinerKeepsCombustibleUseWithoutAuthoritativeSideEffects(bool wood)
    {
        using var fixture = new Fixture(RuntimeSide.Client);
        fixture.Target(
            wood ? BuiltInBlockRegistry.GetIndex<MimosaWoodBlock>() : BuiltInBlockRegistry.GetIndex<GrassBlock>(),
            true
        );
        var before = fixture.Chunk.NetworkContentRevision;
        Assert.True(fixture.Miner.Use(fixture.Ray));
        Assert.Equal(before, fixture.Chunk.NetworkContentRevision);
        Assert.Empty(fixture.Fire.FirePoints);
        Assert.Equal(5, fixture.Inventory.GetSlotCount(0));
        Assert.Equal(0, fixture.Sounds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AuthorityUsesRealFireRulesAndConsumesOnlySuccessfulIgnition(bool combustible)
    {
        using var fixture = new Fixture(RuntimeSide.Authority);
        fixture.Target(BuiltInBlockRegistry.GetIndex<GrassBlock>(), combustible);
        Assert.Equal(combustible, fixture.Miner.Use(fixture.Ray));
        Assert.Equal(combustible ? 4 : 5, fixture.Inventory.GetSlotCount(0));
        Assert.Equal(combustible ? 1 : 0, fixture.Sounds);
        Assert.Equal(combustible, fixture.Fire.FirePoints.Count > 0);
        Assert.Equal(combustible ? 104 : 0, fixture.World.Terrain.GetCellContents(8, 66, 8));
    }

    [Fact]
    public void ReplicaRejectsNoncombustibleTerrainWithoutSideEffects()
    {
        using var fixture = new Fixture(RuntimeSide.Client);
        fixture.Target(BuiltInBlockRegistry.GetIndex<GrassBlock>(), false);
        Assert.False(fixture.Miner.Use(fixture.Ray));
        Assert.Empty(fixture.Fire.FirePoints);
        Assert.Equal(0, fixture.Sounds);
        Assert.Equal(5, fixture.Inventory.GetSlotCount(0));
    }

    [Fact]
    public void ReplicaExplosivePreviewDoesNotPlayAuthorityMatchSound()
    {
        using var fixture = new Fixture(RuntimeSide.Client);
        fixture.Target(BuiltInBlockRegistry.GetIndex<LargeGunpowderKegBlock>(), false);
        Assert.True(fixture.Miner.Use(fixture.Ray));
        Assert.Equal(0, fixture.Sounds);
        Assert.Equal(5, fixture.Inventory.GetSlotCount(0));
    }

    [Theory]
    [InlineData(RuntimeSide.Client)]
    [InlineData(RuntimeSide.Authority)]
    public void BodyUseIsRecognizedButOnlyAuthorityBurnsConsumesAndProducesSound(RuntimeSide side)
    {
        using var fixture = new Fixture(side);
        var fire = fixture.Body(true);
        Assert.True(fixture.Miner.Use(fixture.BodyRay));
        Assert.Equal(side == RuntimeSide.Authority, fire!.IsOnFire);
        Assert.Equal(side == RuntimeSide.Authority ? 4 : 5, fixture.Inventory.GetSlotCount(0));
        Assert.Equal(side == RuntimeSide.Authority ? 1 : 0, fixture.Sounds);
    }

    [Fact]
    public void BodyWithoutFireComponentIsNotAUsableMatchTarget()
    {
        using var fixture = new Fixture(RuntimeSide.Client);
        fixture.Body(false);
        Assert.False(fixture.Miner.Use(fixture.BodyRay));
        Assert.Equal(0, fixture.Sounds);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly WorldCatalogScope _scope;

        private readonly SubsystemBodies _bodies = new();

        internal RuntimeTestProject Project => _scope.Project;

        internal SubsystemTerrain World { get; } = new() { Terrain = new Terrain() };

        internal SubsystemFireBlockBehavior Fire { get; } = new();

        internal ComponentMiner Miner { get; } = new();

        internal MatchInventory Inventory { get; } = new();

        internal TerrainChunk Chunk { get; }

        internal Ray3 Ray { get; } = new(new Vector3(8.5f, 67.5f, 8.5f), -Vector3.UnitY);

        internal Ray3 BodyRay { get; } = new(new Vector3(8.5f, 67.5f, 8.5f), Vector3.UnitZ);

        internal int Sounds { get; private set; }

        internal Fixture(RuntimeSide side)
        {
            _scope = new WorldCatalogScope(
                new NetNode(
                    NetNodeOptions.Default,
                    side == RuntimeSide.Client,
                    PackageHandlerCatalog.Empty,
                    side == RuntimeSide.Authority ? NodeRole.Authority : NodeRole.Client
                )
            );
            Chunk = World.Terrain.AllocateChunk(0, 0);
            Chunk.HasContents = true;
            Project.AddSubsystem(World);
            typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.CellAuthority))!.SetValue(
                World,
                new TerrainCellAuthority(World.Terrain)
            );
            if (side == RuntimeSide.Client)
            {
                typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.ClientChunkContents))!.SetValue(
                    World,
                    new ClientChunkContentCoordinator(World.Terrain)
                );
            }

            Project.AddSubsystem(new SubsystemSky());
            var calculator = new TerrainLightingCalculator(
                World.Terrain,
                _scope.Catalog,
                side == RuntimeSide.Client,
                chunk => chunk.HasContents
            );
            typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.Computations))!.SetValue(
                World,
                new TerrainComputationScheduler(World, calculator)
            );
            var info = new SubsystemGameInfo
                { WorldSettings = new WorldSettings { GameMode = GameMode.Creative, CreativeReach = 7.5f } };
            Project.AddSubsystem(info);
            Project.AddSubsystem(_bodies);
            Project.AddSubsystem(new SubsystemNetworkInterest());
            var sound = new SubsystemSpatialSoundRequests();
            Project.AddSubsystem(sound);
            sound.Requested += _ => Sounds++;
            Project.AddSubsystem(Fire);
            BindTerrain(Fire, World);
            var explosives = new SubsystemExplosivesBlockBehavior();
            Project.AddSubsystem(explosives);
            Set(explosives, "_subsystemTerrain", World);
            var match = new SubsystemMatchBlockBehavior();
            Project.AddSubsystem(match);
            match.Load(new ValuesDictionary());
            var behaviors = new SubsystemBlockBehaviors();
            var table = Enumerable.Range(0, BlockRuntimeCatalog.Capacity)
                .Select(_ => Array.Empty<SubsystemBlockBehavior>()).ToArray();
            table[BuiltInBlockRegistry.GetIndex<MatchBlock>()] = [match];
            Set(behaviors, "_blockBehaviorsByContents", table);
            Set(World, "_subsystemBlockBehaviors", behaviors);
            var creature = new ComponentCreature
            {
                ComponentBody = new ComponentBody { Position = Ray.Position, BoxSize = new Vector3(.6f, 1.8f, .6f) },
                ComponentCreaturePose = new ComponentCreaturePose()
            };
            Project.InitializeComponent(creature);
            Set(creature.ComponentCreaturePose, "_creature", creature);
            Project.InitializeComponent(Inventory);
            Inventory.Put(Terrain.MakeBlockValue(BuiltInBlockRegistry.GetIndex<MatchBlock>()));
            Project.InitializeComponent(Miner);
            typeof(ComponentMiner).GetProperty(nameof(ComponentMiner.ComponentCreature))!.SetValue(Miner, creature);
            typeof(ComponentMiner).GetProperty(nameof(ComponentMiner.Inventory))!.SetValue(Miner, Inventory);
            Set(Miner, "_subsystemGameInfo", info);
            Set(Miner, "_subsystemTerrain", World);
            Set(Miner, "_subsystemBodies", _bodies);
            Set(Miner, "_subsystemMovingBlocks", new SubsystemMovingBlocks());
            Set(Miner, "_subsystemBlockBehaviors", behaviors);
        }

        internal void Target(int contents, bool combustible)
        {
            _scope.Catalog.Blocks[contents].FireDuration = combustible ? 10 : 0;
            if (_scope.Catalog.Blocks[contents] is GunpowderKegBlock keg)
            {
                // Unit raycast geometry; no model/graphics loader is needed to test ownership.
                keg.CollisionBoxes = Block.DefaultCollisionBoxes;
            }

            World.Terrain.SetCellValueFast(8, 65, 8, Terrain.MakeBlockValue(contents));
            Assert.Equal(
                new Point3(8, 65, 8),
                Miner.Raycast<TerrainRaycastResult>(Ray, RaycastMode.Digging)!.Value.CellFace.Point
            );
        }

        internal ComponentOnFire? Body(bool burnable)
        {
            var body = new ComponentBody { Position = new Vector3(8.5f, 66.5f, 10.5f), BoxSize = new Vector3(1, 2, 1) };
            Project.InitializeComponent(body);
            body.Entity.EntityId = 2;
            var components =
                (List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(body.Entity)!;
            components.Add(body);
            ComponentOnFire? fire = null;
            if (burnable)
            {
                fire = new ComponentOnFire();
                Project.InitializeComponent(fire);
                typeof(Component).GetProperty(nameof(Component.Entity))!.SetValue(fire, body.Entity);
                components.Add(fire);
            }

            _bodies.OnEntityAdded(body.Entity);
            return fire;
        }

        public void Dispose() => _scope.Dispose();
    }

    private static void Set(object owner, string field, object value) =>
        owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, value);

    private static void BindTerrain(SubsystemBlockBehavior owner, SubsystemTerrain terrain) =>
        typeof(SubsystemBlockBehavior).GetProperty("SubsystemTerrain", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(owner, terrain);

    private sealed class MatchInventory : ComponentInventoryBase
    {
        public override int ActiveSlotIndex { get; set; }

        public override int GetSlotCapacity(int slotIndex, int value) => 10;

        internal void Put(int value)
        {
            slots.Clear();
            slots.Add(new Slot { Value = value, Count = 5 });
        }

        public override void OnSlotChange(int slotIndex)
        {
        }
    }
}
