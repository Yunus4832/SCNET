using System.Reflection;

using Engine.Core.Collections;
using Engine.Core.Mathematics;

using Game.Components;
using Game.Modding;
using Game.Network;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.Terrains.Distribution;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class WorldOperationHooksTest
{
    [Fact]
    public void ClosingDuringMovingCollisionStopsTheOuterUpdate()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority, bindRuleHooks: false);
        var world = WorldRuntimeContext.For(project);
        using var terrain = new Terrain();
        terrain.AllocateChunk(0, 0).SetMainThreadState(TerrainChunkState.InvalidLight);
        var subsystemTerrain = new SubsystemTerrain { Terrain = terrain };
        project.InitializeSubsystem(subsystemTerrain);
        var time = new SubsystemTime();
        project.InitializeSubsystem(time);
        var moving = new SubsystemMovingBlocks();
        project.InitializeSubsystem(moving);
        typeof(SubsystemMovingBlocks).GetField("_subsystemTerrain", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(moving, subsystemTerrain);
        typeof(SubsystemMovingBlocks).GetField("_subsystemTime", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(moving, time);
        moving.AddMovingBlockSet(
            Vector3.Zero,
            Vector3.Zero,
            0f,
            0f,
            0f,
            Vector2.Zero,
            [],
            string.Empty,
            new object(),
            false
        );
        moving.AddMovingBlockSet(
            Vector3.Zero,
            Vector3.Zero,
            0f,
            0f,
            0f,
            Vector2.Zero,
            [],
            string.Empty,
            new object(),
            false
        );
        var collisions = 0;
        var stopped = 0;
        moving.Stopped += _ => stopped++;
        using var hooks = new RuntimeScope(context => context.Gameplay.OnMovingBlockSetTerrainCollision(_ =>
                {
                    collisions++;
                    world.Close();
                }
            )
        );
        hooks.Bind(project);

        moving.Update(0f);
        moving.Update(0f);

        Assert.Equal(1, collisions);
        Assert.Equal(0, stopped);
    }

    [Fact]
    public void ClosingDuringInjuryPreventsHealthMutation()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority, bindRuleHooks: false);
        var world = WorldRuntimeContext.For(project);
        var health = new ComponentHealth { Health = 1f };
        project.InitializeComponent(health);
        using var hooks = new RuntimeScope(context => context.Gameplay.OnCreatureInjuring(_ => world.Close()));
        hooks.Bind(project);

        health.Injure(.25f, null, false, LocalizedText.Literal("test"));

        Assert.Equal(1f, health.Health);
    }

    [Fact]
    public void ClosingDuringIgnitionPreventsTerrainAccessAndWrites()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority, bindRuleHooks: false);
        var world = WorldRuntimeContext.For(project);
        var fire = new SubsystemFireBlockBehavior();
        project.InitializeSubsystem(fire);
        var terrain = new SubsystemTerrain();
        project.InitializeSubsystem(terrain);
        typeof(SubsystemBlockBehavior).GetProperty("SubsystemTerrain", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(fire, terrain);
        using var hooks = new RuntimeScope(context => context.Gameplay.OnCellIgniting(_ => world.Close()));
        hooks.Bind(project);

        Assert.False(fire.SetCellOnFire(1, 65, 2, 1f));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosingDuringTerrainChangePreventsWritesAndDropWork(bool dispose)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority, bindRuleHooks: false);
        var world = WorldRuntimeContext.For(project);
        using var hooks = new RuntimeScope(context => context.Gameplay.OnTerrainCellChanging(_ =>
                {
                    if (dispose) { project.Dispose(); }
                    else { world.Close(); }
                }
            )
        );
        hooks.Bind(project);
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        terrain.SetCellValueFast(1, 65, 2, 2);
        var subsystem = new SubsystemTerrain { Terrain = terrain };
        project.InitializeSubsystem(subsystem);
        typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.CellAuthority))!
            .SetValue(subsystem, new TerrainCellAuthority(terrain));

        subsystem.DestroyCell(0, 1, 65, 2, 0, false, false);

        Assert.Equal(2, terrain.GetCellValue(1, 65, 2));
        Assert.Equal(0, chunk.ModificationCounter);
    }

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
                    }
                );
                context.BlockBehaviors.OnItemHarvested(_ => throw new InvalidOperationException("Must not harvest."));
            }
        );
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        terrain.SetCellValueFast(1, 2, 3, 2);
        using var project = new RuntimeTestProject(RuntimeSide.Authority, bindRuleHooks: false);
        runtime.Bind(project);
        var subsystem = new SubsystemTerrain { Terrain = terrain };
        project.InitializeSubsystem(subsystem);
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
                    }
                );
                context.Gameplay.OnExplosionPointProcessing(processing =>
                    {
                        explosions++;
                        Assert.Equal(new Point3(1, 2, 3), processing.Point);
                        processing.Cancel = true;
                    }
                );
            }
        );
        using var project = new RuntimeTestProject(RuntimeSide.Authority, bindRuleHooks: false);
        runtime.Bind(project);
        var fire = new SubsystemFireBlockBehavior();
        project.InitializeSubsystem(fire);
        var terrain = new SubsystemTerrain();
        project.InitializeSubsystem(terrain);
        typeof(SubsystemBlockBehavior).GetProperty("SubsystemTerrain", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(fire, terrain);
        var explosion = new SubsystemExplosions();
        project.InitializeSubsystem(explosion);
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
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var body = new ComponentBody();
        project.InitializeComponent(body);
        var boxes = new DynamicArray<ComponentBody.CollisionBox>();
        host.LoadAndStart(
            [
                new ModDescriptor(
                    new ModManifest("test.operations", "Operations", "1.0.0"),
                    () => new CallbackMod(context => context.Gameplay.OnTerrainCollisionBoxes(query =>
                            {
                                Assert.Same(body, query.Body);
                                Assert.Equal(bounds, query.Bounds);
                                query.CollisionBoxes.Add(new ComponentBody.CollisionBox { Box = bounds });
                            }
                        )
                    )
                )
            ]
        );
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
                }
            )
        );
        using var project = new RuntimeTestProject(RuntimeSide.Authority, bindRuleHooks: false);
        runtime.Bind(project);
        var body = new ComponentBody();
        project.InitializeComponent(body);
        var rider = new ComponentRider { ComponentCreature = new ComponentCreature { ComponentBody = body } };

        project.InitializeComponent(rider);
        var mount = new ComponentMount();
        project.InitializeComponent(mount);
        rider.StartMounting(mount);

        Assert.Equal(1, calls);
        Assert.Null(body.ParentBody);
        Assert.False(
            (bool)typeof(ComponentRider).GetField("_isAnimating", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(rider)!
        );
    }

    [Fact]
    public void EveryOperationRegistrationIsFrozenAndRemovedWithItsOwner()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var subsystems = new Dictionary<Type, EntitySystem.Core.Subsystem>();

        T Subsystem<T>(T value) where T : EntitySystem.Core.Subsystem
        {
            if (subsystems.TryGetValue(typeof(T), out var existing)) { return (T)existing; }

            project.InitializeSubsystem(value);
            subsystems.Add(typeof(T), value);
            return value;
        }

        T Component<T>(T value) where T : EntitySystem.Core.Component
        {
            project.InitializeComponent(value);
            return value;
        }

        var calls = 0;
        var host = new ModHost();
        host.LoadAndStart(
            [
                new ModDescriptor(
                    new ModManifest("test.operations", "Operations", "1.0.0"),
                    () => new CallbackMod(context =>
                        {
                            context.Gameplay.OnCellIgniting(_ => calls++);
                            context.Gameplay.OnExplosionPointProcessing(_ => calls++);
                            context.Gameplay.OnTerrainCollisionBoxes(_ => calls++);
                            context.Gameplay.OnMounting(_ => calls++);
                            context.Gameplay.OnPistonBlockMoving(_ => calls++);
                            context.Gameplay.OnMovingBlockSetTerrainCollision(_ => calls++);
                        }
                    )
                )
            ]
        );

        void DispatchAll()
        {
            host.Gameplay.Invoke(new CellIgnitingContext(Subsystem(new SubsystemTerrain()), Point3.Zero, null));
            host.Gameplay.Invoke(
                new ExplosionPointProcessingContext(Subsystem(new SubsystemExplosions()), Point3.Zero, null)
            );
            host.Gameplay.Invoke(new TerrainCollisionBoxesContext(Component(new ComponentBody()), default, []));
            host.Gameplay.Invoke(new MountingContext(Component(new ComponentRider()), Component(new ComponentMount())));
            host.Gameplay.Invoke(
                new PistonBlockMovingContext(
                    Subsystem(new SubsystemPistonBlockBehavior()),
                    Point3.Zero,
                    Point3.Zero,
                    false
                )
            );
            host.Gameplay.Invoke(
                new MovingBlockSetTerrainCollisionContext(
                    Subsystem(new SubsystemMovingBlocks()),
                    null!,
                    Point3.Zero,
                    Point3.Zero
                )
            );
        }

        try
        {
            Assert.True(host.Gameplay.HasExplosionPointProcessingHandlers);
            Assert.True(host.Gameplay.HasTerrainCollisionBoxesHandlers);
            Assert.Throws<InvalidOperationException>(() =>
                host.Runtimes[0].Context.Gameplay.OnMounting(_ => { })
            );
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
        private readonly NodeRole _previousNodeRole = CommonLib.Net.Role;

        private readonly GameModRuntime _runtime;

        public RuntimeScope(Action<IModContext> configure)
        {
            _runtime = GameModRuntime.Start(
                [
                    new ModDescriptor(
                        new ModManifest("test.operations", "Operations", "1.0.0"),
                        () => new CallbackMod(configure)
                    )
                ]
            );

            CommonLib.Net.Role = NodeRole.Authority;
        }

        public void Bind(RuntimeTestProject project) =>
            project.BindRuleHooks(_runtime.Gameplay, _runtime.BlockBehaviors);

        public void Dispose()
        {
            CommonLib.Net.Role = _previousNodeRole;
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
