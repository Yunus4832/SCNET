using System.Reflection;

using Game.Blocks;
using Game.Components;
using Game.Inventories;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Subsystems;
using Game.Widgets;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class InventoryOwnershipTest
{
    private static int _nextInventoryId;

    [Fact]
    public void AuthorityMovesOnlyTheAvailableCapacityAndConservesItems()
    {
        using var scope = new WorldCatalogScope();
        Configure(scope);
        var source = Inventory(scope, 1, 5);
        var target = Inventory(scope, 1, 8);
        Assert.Equal(2, InventoryOperations.GetMoveCount(source, 0, target, 0, 5));
        Assert.True(InventoryOperations.Move(source, 0, target, 0, 5));
        Assert.Equal(3, source.GetSlotCount(0));
        Assert.Equal(10, target.GetSlotCount(0));
        Assert.Equal(1, source.Changes);
        Assert.Equal(1, target.Changes);
    }

    [Fact]
    public void AuthorityDragDropSwapsWholeStacksAndProcessingConsumesOnlyItsCapacity()
    {
        using var scope = new WorldCatalogScope();
        Configure(scope);
        var source = Inventory(scope, 1, 3);
        var target = Inventory(scope, 2, 2);
        Assert.True(InventoryOperations.DragDrop(source, 0, DragMode.AllItems, target, 0, false));
        Assert.Equal(2, source.GetSlotValue(0));
        Assert.Equal(2, source.GetSlotCount(0));
        Assert.Equal(1, target.GetSlotValue(0));
        Assert.Equal(3, target.GetSlotCount(0));
        target.ProcessCapacity = 1;
        Assert.True(InventoryOperations.DragDrop(source, 0, DragMode.AllItems, target, 0, true));
        Assert.Equal(1, target.ProcessCalls);
        Assert.Equal(1, source.GetSlotCount(0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClientUiQueuesOneRequestWithoutMutatingOrProcessingItsReplica(bool process)
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        using var scope = new WorldCatalogScope(node);
        Configure(scope);
        var connection = new ConnectedStub();
        node.Server = new Client(Guid.Empty) { Connection = connection };
        var source = Inventory(scope, 1, 5);
        var target = Inventory(scope, 1, 8);
        target.ProcessCapacity = process ? 1 : 0;
        Assert.True(
            process
                ? InventorySlotWidget.HandleDragDrop(source, 0, DragMode.SingleItem, target, 0, true)
                : InventorySlotWidget.HandleMoveItem(source, 0, target, 0, 5)
        );
        Assert.Equal(1, node.PendingPackageCount);
        Assert.Equal(5, source.GetSlotCount(0));
        Assert.Equal(8, target.GetSlotCount(0));
        Assert.Equal(0, source.Changes);
        Assert.Equal(0, target.ProcessCalls);
        typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(
            node,
            null
        );
        if (process)
        {
            var request = Assert.IsType<InventoryDragDropRequestPackage>(connection.Sent);
            Assert.True(request.ProcessingOnly);
            Assert.Equal(source.Id, request.Source!.InventoryId);
            Assert.Equal(target.Id, request.Target!.InventoryId);
        }
        else
        {
            var request = Assert.IsType<InventoryMoveRequestPackage>(connection.Sent);
            Assert.Equal(2, request.Count);
            Assert.Equal(source.Id, request.Source!.InventoryId);
            Assert.Equal(target.Id, request.Target!.InventoryId);
        }

        Assert.Throws<InvalidOperationException>(() => InventoryOperations.Move(source, 0, target, 0, 1));
    }

    [Fact]
    public void InvalidSlotsAndSelfTransfersCannotConsumeItems()
    {
        using var scope = new WorldCatalogScope();
        Configure(scope);
        var inventory = Inventory(scope, 1, 5);
        Assert.False(InventoryOperations.Move(inventory, 0, inventory, 0, 5));
        Assert.False(InventoryOperations.DragDrop(inventory, 0, DragMode.AllItems, inventory, 0, true));
        Assert.Equal(0, InventoryOperations.GetMoveCount(inventory, -1, inventory, 0, 1));
        Assert.Equal(0, inventory.Changes);
    }

    private static void Configure(WorldCatalogScope scope)
    {
        scope.Project.AddSubsystem(new SubsystemGameInfo { WorldSettings = new WorldSettings() });
        scope.Catalog.Blocks[1] = new GraniteBlock();
        scope.Catalog.Blocks[2] = new GraniteBlock();
    }

    private static TestInventory Inventory(WorldCatalogScope scope, int value, int count)
    {
        var inventory = new TestInventory(value, count);
        scope.Project.InitializeComponent(inventory);
        // Model IDs already assigned by authority loading; the fixture does not run inventory Load.
        typeof(ComponentInventoryBase).GetProperty(nameof(IInventory.Id))!.SetValue(
            inventory,
            Interlocked.Increment(ref _nextInventoryId)
        );
        return inventory;
    }

    private sealed class TestInventory : ComponentInventoryBase
    {
        internal int Changes { get; private set; }

        internal int ProcessCalls { get; private set; }

        internal int ProcessCapacity { get; set; }

        internal TestInventory(int value, int count) => slots.Add(new Slot { Value = value, Count = count });

        public override int GetSlotCapacity(int slotIndex, int value) => 10;

        public override int GetSlotProcessCapacity(int slotIndex, int value) => ProcessCapacity;

        public override void OnSlotChange(int slotIndex) => Changes++;

        public override void ProcessSlotItems(
            IInventory sourceInventory,
            int sourceSlotIndex,
            int slotIndex,
            int value,
            int count,
            int processCount,
            out int processedValue,
            out int processedCount
        )
        {
            ProcessCalls++;
            processedValue = 0;
            processedCount = 0;
        }
    }

    private sealed class ConnectedStub : IMessageConnection
    {
        internal IProtocolMessage? Sent { get; private set; }

        public bool IsConnected => true;

        public bool IsInProcess => true;

        public ConnectionSendResult TrySendOwned(OutboundPackage message)
        {
            Sent = message.Package;
            return ConnectionSendResult.Accepted;
        }

        public void Disconnect(LocalizedText reason)
        {
        }

        public void Dispose()
        {
        }
    }
}
