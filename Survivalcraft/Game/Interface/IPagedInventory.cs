namespace Game;

public interface IPagedInventory : IInventory
{
    int HotbarSlotsCount { get; }
}

public static class PagedInventoryExtensions
{
    public static int GetHotbarPageIndex(this IPagedInventory inventory)
    {
        return inventory.ActiveSlotIndex / inventory.VisibleSlotsCount;
    }

    public static int GetHotbarPagesCount(this IPagedInventory inventory)
    {
        return (inventory.HotbarSlotsCount + inventory.VisibleSlotsCount - 1) / inventory.VisibleSlotsCount;
    }

    public static int GetHotbarSlotIndex(this IPagedInventory inventory, int pageSlotIndex)
    {
        var slotIndex = inventory.GetHotbarPageIndex() * inventory.VisibleSlotsCount + pageSlotIndex;
        return pageSlotIndex >= 0 && pageSlotIndex < inventory.VisibleSlotsCount &&
               slotIndex < inventory.HotbarSlotsCount
            ? slotIndex
            : -1;
    }

    public static void SelectHotbarPageSlot(this IPagedInventory inventory, int pageSlotIndex)
    {
        var slotIndex = inventory.GetHotbarSlotIndex(pageSlotIndex);
        if (slotIndex >= 0)
        {
            inventory.ActiveSlotIndex = slotIndex;
        }
    }

    public static void ChangeHotbarPage(this IPagedInventory inventory, int offset)
    {
        var pageSlotIndex = inventory.ActiveSlotIndex % inventory.VisibleSlotsCount;
        var pageIndex = MathUtils.Clamp(inventory.GetHotbarPageIndex() + offset, 0,
            inventory.GetHotbarPagesCount() - 1);
        var pageSlotsCount = MathUtils.Min(inventory.VisibleSlotsCount,
            inventory.HotbarSlotsCount - pageIndex * inventory.VisibleSlotsCount);
        inventory.ActiveSlotIndex = pageIndex * inventory.VisibleSlotsCount +
                                    MathUtils.Min(pageSlotIndex, pageSlotsCount - 1);
    }

    public static void SelectHotbarPage(this IPagedInventory inventory, int pageIndex)
    {
        if (pageIndex >= 0 && pageIndex < inventory.GetHotbarPagesCount())
        {
            inventory.ChangeHotbarPage(pageIndex - inventory.GetHotbarPageIndex());
        }
    }
}
