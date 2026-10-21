using Game.Components;

namespace Survivalcraft.Test.Network;

public sealed class InventorySnapshotCaptureTest
{
    [Fact]
    public void RepeatedSlotRequestsAreDeduplicatedAndQueuedValuesAreStable()
    {
        var inventory = new TestInventory();
        var package = InventoryPackageBuilder.Capture(
            new Dictionary<Game.IInventory, List<int>>
                { [inventory] = [0, 0] }
        );
        inventory.Change(8, 99);
        var slot = Assert.Single(Assert.Single(package.Slots).Value);
        Assert.Equal(0, slot.SlotIndex);
        Assert.Equal(3, slot.Count);
        Assert.Equal(7, slot.Value);
    }

    private sealed class TestInventory : ComponentInventoryBase
    {
        public TestInventory() => slots.Add(new Slot { Count = 3, Value = 7 });

        public void Change(int count, int value)
        {
            slots[0].Count = count;
            slots[0].Value = value;
        }
    }
}
