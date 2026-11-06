using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Characters;
using Game.Components;
using Game.ElectricElements;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Subsystems.Circuits;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class CircuitEditPresentationBoundaryTest
{
    [Theory]
    [InlineData(BuiltInBlockEditKind.Battery, true)]
    [InlineData(BuiltInBlockEditKind.Piston, true)]
    [InlineData(BuiltInBlockEditKind.AdjustableDelayGate, true)]
    [InlineData(BuiltInBlockEditKind.MemoryBank, true)]
    [InlineData(BuiltInBlockEditKind.TruthTable, true)]
    [InlineData(BuiltInBlockEditKind.Battery, false)]
    [InlineData(BuiltInBlockEditKind.Piston, false)]
    [InlineData(BuiltInBlockEditKind.AdjustableDelayGate, false)]
    [InlineData(BuiltInBlockEditKind.MemoryBank, false)]
    [InlineData(BuiltInBlockEditKind.TruthTable, false)]
    public void RuleEditorEntrypointsPublishOnlyForLocalClientActors(BuiltInBlockEditKind kind, bool local)
    {
        using var project = new RuntimeTestProject(local ? RuntimeSide.Client : RuntimeSide.Authority);
        var character = Character(project, local);
        var behavior = Behavior(kind);
        project.InitializeSubsystem(behavior);
        var inventory = new CircuitInventory();
        project.InitializeComponent(inventory);
        inventory.Put(Terrain.MakeBlockValue(Game.Network.Handling.EditableBlockPackageHandler.GetBlockContents(kind)));
        CircuitInventoryEditRequest? item = null;
        CircuitBlockEditRequest? block = null;
        Subscribe(behavior, request => item = request, request => block = request);
        Assert.True(behavior.OnEditInventoryItem(inventory, 0, character));
        Assert.True(behavior.OnEditBlock(1, 2, 3, inventory.GetSlotValue(0), character));
        if (local)
        {
            var actualItem = Assert.IsType<CircuitInventoryEditRequest>(item);
            var actualBlock = Assert.IsType<CircuitBlockEditRequest>(block);
            Assert.Equal(kind, actualItem.Kind);
            Assert.Same(inventory, actualItem.Inventory);
            Assert.Equal(inventory.GetSlotValue(0), actualItem.OriginalValue);
            Assert.Equal(new Point3(1, 2, 3), actualBlock.Point);
            Assert.Equal(kind, actualBlock.Kind);
        }
        else
        {
            Assert.Null(item);
            Assert.Null(block);
        }
    }

    [Fact]
    public void CancelledWorkingCopiesCannotChangeWorldMemoryOrTruthTableState()
    {
        var memory = new MemoryBankData { LastOutput = 9 };
        memory.Data.Add(7);
        var truth = new TruthTableData();
        truth.Data[0] = 5;
        var memoryEdit = SubsystemCircuitEditPresentation.CreateMemoryBankWorkingCopy(memory);
        var truthEdit = SubsystemCircuitEditPresentation.CreateTruthTableWorkingCopy(truth);
        memoryEdit.Data.Array[0] = 3;
        memoryEdit.LastOutput = 1;
        truthEdit.Data[0] = 2;
        Assert.Equal(7, memory.Data.Array[0]);
        Assert.Equal(9, memory.LastOutput);
        Assert.Equal(5, truth.Data[0]);
        Assert.NotSame(memory.Data, memoryEdit.Data);
        Assert.NotSame(truth.Data, truthEdit.Data);
    }

    [Fact]
    public void CallbackChecksRejectChangedSlotChangedBlockOtherWorldAndClosedWorld()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        using var other = new RuntimeTestProject(RuntimeSide.Client);
        var character = Character(project, true);
        var inventory = new CircuitInventory();
        project.InitializeComponent(inventory);
        var value = Terrain.MakeBlockValue(
            Game.Network.Handling.EditableBlockPackageHandler.GetBlockContents(BuiltInBlockEditKind.Battery)
        );
        inventory.Put(value);
        var request = new CircuitInventoryEditRequest(character, inventory, 0, value, BuiltInBlockEditKind.Battery);
        Assert.True(SubsystemCircuitEditPresentation.CanEditInventory(project, request));
        Assert.False(SubsystemCircuitEditPresentation.CanEditInventory(other, request));
        inventory.Put(Terrain.ReplaceData(value, 1));
        Assert.False(SubsystemCircuitEditPresentation.CanEditInventory(project, request));
        using var terrain = new Terrain();
        terrain.AllocateChunk(0, 0);
        terrain.SetCellValueFast(1, 2, 3, value);
        var subsystemTerrain = new SubsystemTerrain { Terrain = terrain };
        var blockRequest = new CircuitBlockEditRequest(
            character,
            new Point3(1, 2, 3),
            value,
            BuiltInBlockEditKind.Battery
        );
        Assert.True(SubsystemCircuitEditPresentation.CanEditBlock(project, subsystemTerrain, blockRequest));
        terrain.SetCellValueFast(1, 2, 3, 0);
        Assert.False(SubsystemCircuitEditPresentation.CanEditBlock(project, subsystemTerrain, blockRequest));
        inventory.Put(value);
        WorldRuntimeContext.For(project).Close();
        Assert.False(SubsystemCircuitEditPresentation.CanEditInventory(project, request));
    }

    [Fact]
    public void ClientOwnerUnsubscribesAllFivePublishersOnDispose()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        project.AddSubsystem(new SubsystemTerrain());
        project.AddSubsystem(new SubsystemItemsScanner());
        project.AddSubsystem(new SubsystemNetworkInterest());
        var behaviors = Enum.GetValues<BuiltInBlockEditKind>()
            .Where(kind => kind is BuiltInBlockEditKind.Battery or BuiltInBlockEditKind.Piston or
                BuiltInBlockEditKind.AdjustableDelayGate or BuiltInBlockEditKind.MemoryBank
                or BuiltInBlockEditKind.TruthTable
            )
            .Select(Behavior).ToArray();
        var empty = new ValuesDictionary();
        empty.SetValue("Blocks", new ValuesDictionary());
        empty.SetValue("Items", new ValuesDictionary());
        foreach (var behavior in behaviors)
        {
            project.AddSubsystem(behavior);
            if (behavior is SubsystemMemoryBankBlockBehavior or SubsystemTruthTableCircuitBlockBehavior)
            {
                behavior.Load(empty);
            }
        }

        var owner = new SubsystemCircuitEditPresentation();
        project.InitializeSubsystem(owner);
        owner.Load(new ValuesDictionary());
        foreach (var behavior in behaviors) { Assert.NotNull(Field(behavior, "InventoryEditRequested")); }

        owner.Dispose();
        owner.Dispose();
        foreach (var behavior in behaviors)
        {
            Assert.Null(Field(behavior, "InventoryEditRequested"));
            Assert.Null(Field(behavior, "BlockEditRequested"));
        }
    }

    private static ComponentCharacter Character(RuntimeTestProject project, bool local)
    {
        var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(data, local);
        var character = new ComponentCharacter { CharacterData = data };
        project.InitializeComponent(character);
        return character;
    }

    private static SubsystemBlockBehavior Behavior(BuiltInBlockEditKind kind)
    {
        return kind switch
        {
            BuiltInBlockEditKind.Battery => new SubsystemBatteryBlockBehavior(),
            BuiltInBlockEditKind.Piston => new SubsystemPistonBlockBehavior(),
            BuiltInBlockEditKind.AdjustableDelayGate => new SubsystemAdjustableDelayGateBlockBehavior(),
            BuiltInBlockEditKind.MemoryBank => new SubsystemMemoryBankBlockBehavior(),
            BuiltInBlockEditKind.TruthTable => new SubsystemTruthTableCircuitBlockBehavior(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static void Subscribe(
        SubsystemBlockBehavior behavior,
        Action<CircuitInventoryEditRequest> inventory,
        Action<CircuitBlockEditRequest> block
    )
    {
        behavior.GetType().GetEvent("InventoryEditRequested")!.AddEventHandler(behavior, inventory);
        behavior.GetType().GetEvent("BlockEditRequested")!.AddEventHandler(behavior, block);
    }

    private static object? Field(object instance, string name)
    {
        return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance);
    }

    private sealed class CircuitInventory : ComponentInventoryBase
    {
        public void Put(int value)
        {
            slots.Clear();
            slots.Add(new Slot { Value = value, Count = 1 });
        }
    }
}
