using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Components;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class BlockInventoryDestructionOwnershipTest
{
    [Theory]
    [InlineData(typeof(SubsystemChestBlockBehavior), RuntimeSide.Authority)]
    [InlineData(typeof(SubsystemChestBlockBehavior), RuntimeSide.Client)]
    [InlineData(typeof(SubsystemCraftingTableBlockBehavior), RuntimeSide.Authority)]
    [InlineData(typeof(SubsystemCraftingTableBlockBehavior), RuntimeSide.Client)]
    [InlineData(typeof(SubsystemDispenserBlockBehavior), RuntimeSide.Authority)]
    [InlineData(typeof(SubsystemDispenserBlockBehavior), RuntimeSide.Client)]
    [InlineData(typeof(SubsystemFurnaceBlockBehavior), RuntimeSide.Authority)]
    [InlineData(typeof(SubsystemFurnaceBlockBehavior), RuntimeSide.Client)]
    public void TerrainRemovalDropsInventoryAndRemovesEntityOnlyOnAuthority(Type behaviorType, RuntimeSide side)
    {
        using var project = new RuntimeTestProject(side) { PostponeFireEntityAddedEvents = false };
        var blocks = new SubsystemBlockEntities();
        project.AddSubsystem(blocks);
        var terrain = new TestTerrain();
        project.AddSubsystem(terrain);
        var behavior = (SubsystemBlockBehavior)Activator.CreateInstance(behaviorType)!;
        project.InitializeSubsystem(behavior);
        typeof(SubsystemBlockBehavior).GetProperty("SubsystemTerrain", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(behavior, terrain);
        behaviorType.GetField("_subsystemBlockEntities", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(behavior, blocks);
        var block = new ComponentBlockEntity { Coordinates = new Point3(1, 65, 2) };
        project.InitializeComponent(block);
        var inventory = new DropProbe();
        inventory.Initialize(block.Entity, block.ValuesDictionary);
        var components = (List<Component>)typeof(Entity).GetField(
                "_components",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!
            .GetValue(block.Entity)!;
        components.Add(block);
        components.Add(inventory);
        project.AddEntity(block.Entity);
        Assert.Same(block, blocks.GetBlockEntity(1, 65, 2));

        behavior.OnBlockRemoved(0, 0, 1, 65, 2);
        behavior.OnBlockRemoved(0, 0, 1, 65, 2);

        Assert.Equal(side == RuntimeSide.Authority ? 1 : 0, inventory.Drops);
        Assert.Equal(side == RuntimeSide.Client, block.Entity.IsAddedToProject);
        Assert.Equal(side == RuntimeSide.Client, blocks.GetBlockEntity(1, 65, 2) != null);
        if (side == RuntimeSide.Client)
        {
            // The replicated entity removal, rather than the terrain notification, owns replica disposal.
            project.RemoveEntity(block.Entity, true);
            Assert.Null(blocks.GetBlockEntity(1, 65, 2));
            Assert.Equal(0, inventory.Drops);
        }
    }

    private sealed class TestTerrain : SubsystemTerrain
    {
        public override void Dispose()
        {
        }
    }

    private sealed class DropProbe : ComponentInventoryBase
    {
        public int Drops { get; private set; }

        public override void DropAllItems(Vector3 position) => Drops++;
    }
}
