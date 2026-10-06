using Game.Network.Packages.Handlers;

namespace Survivalcraft.Test.Network;

public sealed class InventorySlotBoundaryTest
{
    [Theory]
    [InlineData(-1, 10, false)]
    [InlineData(10, 10, false)]
    [InlineData(0, 0, false)]
    [InlineData(0, -1, false)]
    [InlineData(0, 10, true)]
    [InlineData(9, 10, true)]
    public void RequestsRequireAnExistingSlot(int slot, int capacity, bool valid)
    {
        Assert.Equal(valid, ComponentInventoryPackageHandler.IsValidSlot(slot, capacity));
    }
}
