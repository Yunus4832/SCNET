using System.Reflection;

using Engine.Core;

using Game.Components;
using Game.Modding;
using Game.Network;
using Game.Network.Enums;
using Game.Subsystems;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class WorldOperationHooksTest
{
    [Fact]
    public void DestructionIsCancelledBeforeDropsHarvestingOrTerrainWrites()
    {
        var calls = 0;
        using var runtime = new RuntimeScope(context =>
        {
            context.Gameplay.OnTerrainCellChanging(changing =>
            {
                calls++;
                Assert.Equal(2, changing.OldValue);
                changing.Cancel = true;
            });
            context.BlockBehaviors.OnItemHarvested(_ => throw new InvalidOperationException("Must not harvest."));
        });
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        terrain.SetCellValueFast(1, 2, 3, 2);
        var subsystem = new SubsystemTerrain { Terrain = terrain };
        typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.CellAuthority))!
            .SetValue(subsystem, new TerrainCellAuthority(terrain));
        var revision = chunk.NetworkContentRevision;

        // Drop and particle subsystems deliberately are not initialized: a late veto would access them.
        subsystem.DestroyCell(0, 1, 2, 3, 0, false, false);

        Assert.Equal(1, calls);
        Assert.Equal(2, terrain.GetCellValue(1, 2, 3));
        Assert.Equal(revision, chunk.NetworkContentRevision);
        Assert.Equal(0, chunk.ModificationCounter);
    }

    [Fact]
    public void IgnitionAndExplosionVetoBeforeTheirWorldSideEffects()
    {
        var ignitions = 0;
        var explosions = 0;
        using var runtime = new RuntimeScope(context =>
        {
            context.Gameplay.OnCellIgniting(igniting =>
            {
                ignitions++;
                Assert.Equal(new Point3(1, 2, 3), igniting.Point);
                igniting.Cancel = true;
            });
            context.Gameplay.OnExplosionPointProcessing(processing =>
            {
                explosions++;
                Assert.Equal(new Point3(1, 2, 3), processing.Point);
                processing.Cancel = true;
            });
        });
        var fire = new SubsystemFireBlockBehavior();
        var explosion = new SubsystemExplosions();
        var processed = new SubsystemExplosions.SparseSpatialArray<bool>(0, 0, 0, false);
        var queue = new List<SubsystemExplosions.ProcessPoint>();

        Assert.False(fire.SetCellOnFire(1, 2, 3, 1f));
        explosion.TryAddPoint(1, 2, 3, 0, 10f, true, queue, processed);

        Assert.Equal(1, ignitions);
        Assert.Equal(1, explosions);
        Assert.Empty(queue);
        Assert.False(processed.Get(1, 2, 3));
    }

    [Fact]
    public void CollisionContributionsUseTheQueryBufferAndAreRemovedWithTheOwner()
    {
        var host = new ModHost();
        var bounds = new BoundingBox(new Vector3(0, 300, 0), new Vector3(1, 301, 1));
        var body = new ComponentBody();
        var boxes = new DynamicArray<ComponentBody.CollisionBox>();
        host.LoadAndStart([
            new ModDescriptor(new ModManifest("test.operations", "Operations", "1.0.0"),
                () => new CallbackMod(context => context.Gameplay.OnTerrainCollisionBoxes(query =>
                {
                    Assert.Same(body, query.Body);
                    Assert.Equal(bounds, query.Bounds);
                    query.CollisionBoxes.Add(new ComponentBody.CollisionBox { Box = bounds });
                })))
        ]);
        try
        {
            host.Gameplay.Invoke(new TerrainCollisionBoxesContext(body, bounds, boxes));
            Assert.Single(boxes);
            host.StopAll();
            host.Gameplay.Invoke(new TerrainCollisionBoxesContext(body, bounds, boxes));
            Assert.Single(boxes);
        }
        finally
        {
            host.StopAll();
        }
    }

    [Fact]
    public void MountingVetoRunsBeforeAnimationParentChangesOrNetworkRequests()
    {
        var calls = 0;
        using var runtime = new RuntimeScope(context => context.Gameplay.OnMounting(mounting =>
        {
            calls++;
            mounting.Cancel = true;
        }));
        var body = new ComponentBody();
        var rider = new ComponentRider { ComponentCreature = new ComponentCreature { ComponentBody = body } };

        rider.StartMounting(new ComponentMount());

        Assert.Equal(1, calls);
        Assert.Null(body.ParentBody);
        Assert.False((bool)typeof(ComponentRider).GetField("_isAnimating", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(rider)!);
    }

    [Fact]
    public void EveryOperationRegistrationIsFrozenAndRemovedWithItsOwner()
    {
        var calls = 0;
        var host = new ModHost();
        host.LoadAndStart([
            new ModDescriptor(new ModManifest("test.operations", "Operations", "1.0.0"),
                () => new CallbackMod(context =>
                {
                    context.Gameplay.OnCellIgniting(_ => calls++);
                    context.Gameplay.OnExplosionPointProcessing(_ => calls++);
                    context.Gameplay.OnTerrainCollisionBoxes(_ => calls++);
                    context.Gameplay.OnMounting(_ => calls++);
                    context.Gameplay.OnPistonBlockMoving(_ => calls++);
                    context.Gameplay.OnMovingBlockSetTerrainCollision(_ => calls++);
                }))
        ]);
        void DispatchAll()
        {
            host.Gameplay.Invoke(new CellIgnitingContext(new SubsystemTerrain(), Point3.Zero, null));
            host.Gameplay.Invoke(new ExplosionPointProcessingContext(new SubsystemExplosions(), Point3.Zero, null));
            host.Gameplay.Invoke(new TerrainCollisionBoxesContext(new ComponentBody(), default, []));
            host.Gameplay.Invoke(new MountingContext(new ComponentRider(), new ComponentMount()));
            host.Gameplay.Invoke(new PistonBlockMovingContext(new SubsystemPistonBlockBehavior(), Point3.Zero, Point3.Zero, false));
            host.Gameplay.Invoke(new MovingBlockSetTerrainCollisionContext(new SubsystemMovingBlocks(), null!, Point3.Zero, Point3.Zero));
        }

        try
        {
            Assert.True(host.Gameplay.HasExplosionPointProcessingHandlers);
            Assert.True(host.Gameplay.HasTerrainCollisionBoxesHandlers);
            Assert.Throws<InvalidOperationException>(() =>
                host.Runtimes[0].Context.Gameplay.OnMounting(_ => { }));
            DispatchAll();
            Assert.Equal(6, calls);
            host.StopAll();
            Assert.False(host.Gameplay.HasExplosionPointProcessingHandlers);
            Assert.False(host.Gameplay.HasTerrainCollisionBoxesHandlers);
            DispatchAll();
            Assert.Equal(6, calls);
        }
        finally
        {
            host.StopAll();
        }
    }

    private sealed class RuntimeScope : IDisposable
    {
        private readonly GameModRuntime? _previous = CurrentModRuntime.Value;
        private readonly WorkType _previousWorkType = CommonLib.WorkType;
        private readonly GameModRuntime _runtime;

        public RuntimeScope(Action<IModContext> configure)
        {
            _runtime = GameModRuntime.Start([
                new ModDescriptor(new ModManifest("test.operations", "Operations", "1.0.0"),
                    () => new CallbackMod(configure))
            ]);
            CurrentModRuntime.Set(_runtime);
            CommonLib.WorkType = WorkType.Local;
        }

        public void Dispose()
        {
            CurrentModRuntime.Set(_previous);
            CommonLib.WorkType = _previousWorkType;
            _runtime.Dispose();
        }
    }

    private sealed class CallbackMod(Action<IModContext> configure) : IMod
    {
        public void Configure(IModContext context) => configure(context);
        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }
}
