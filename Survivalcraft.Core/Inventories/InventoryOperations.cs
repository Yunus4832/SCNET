namespace Game.Inventories;

/// <summary>Inventory transfer rules and authority-only execution, independent of widgets and transport.</summary>
public static class InventoryOperations
{
    public static bool IsItemDisabled(IInventory inventory, int value)
    {
        return inventory.Project is null ||
               inventory.Project.IsDisposed ||
               WorldRuntimeContext.For(inventory.Project).IsClosed ||
               BlockAvailability.IsBlockDisabled(
                   WorldContentBinding.For(inventory.Project).Catalog,
                   inventory.Project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.DisableBlocks,
                   value
               );
    }

    private static bool ValidSlots(IInventory source, int sourceSlot, IInventory target, int targetSlot)
    {
        return source.Project is not null &&
               ReferenceEquals(source.Project, target.Project) &&
               !source.Project.IsDisposed &&
               !WorldRuntimeContext.For(source.Project).IsClosed &&
               sourceSlot >= 0 &&
               sourceSlot < source.SlotsCount &&
               targetSlot >= 0 &&
               targetSlot < target.SlotsCount &&
               (!ReferenceEquals(source, target) || sourceSlot != targetSlot) &&
               source.GetSlotCount(sourceSlot) > 0 &&
               !IsItemDisabled(source, source.GetSlotValue(sourceSlot));
    }

    public static int GetMoveCount(IInventory source, int sourceSlot, IInventory target, int targetSlot, int count)
    {
        if (count <= 0 || !ValidSlots(source, sourceSlot, target, targetSlot)) { return 0; }

        var value = source.GetSlotValue(sourceSlot);
        var targetCount = target.GetSlotCount(targetSlot);
        if (targetCount > 0 &&
            !WorldContentBinding.For(source.Project).Catalog.Blocks[Terrain.ExtractContents(value)]
                .CanAutoStack(value, target.GetSlotValue(targetSlot))) { return 0; }

        return Math.Max(
            0,
            MathUtils.Min(
                target.GetSlotCapacity(targetSlot, value) - targetCount,
                source.GetSlotCount(sourceSlot),
                count
            )
        );
    }

    public static bool CanDragDrop(
        IInventory source,
        int sourceSlot,
        DragMode mode,
        IInventory target,
        int targetSlot,
        bool processingOnly
    )
    {
        if (mode is not (DragMode.AllItems or DragMode.SingleItem) ||
            !ValidSlots(source, sourceSlot, target, targetSlot)) { return false; }

        var sourceValue = source.GetSlotValue(sourceSlot);
        if (target.GetSlotProcessCapacity(targetSlot, sourceValue) > 0) { return true; }

        if (processingOnly) { return false; }

        var count = mode == DragMode.SingleItem ? 1 : source.GetSlotCount(sourceSlot);
        if (GetMoveCount(source, sourceSlot, target, targetSlot, count) > 0) { return true; }

        return source.GetSlotCount(sourceSlot) == count &&
               target.GetSlotCapacity(targetSlot, sourceValue) >= count &&
               source.GetSlotCapacity(sourceSlot, target.GetSlotValue(targetSlot)) >= target.GetSlotCount(targetSlot);
    }

    private static void RequireAuthority(IInventory source)
    {
        if (source.Project is null || !WorldRuntimeContext.For(source.Project).IsAuthority)
        {
            throw new InvalidOperationException("Inventory mutations require an authority world.");
        }
    }

    public static bool Move(IInventory source, int sourceSlot, IInventory target, int targetSlot, int count)
    {
        RequireAuthority(source);
        var moved = GetMoveCount(source, sourceSlot, target, targetSlot, count);
        if (moved == 0) { return false; }

        var value = source.GetSlotValue(sourceSlot);
        var removed = source.RemoveSlotItems(sourceSlot, moved);
        target.AddSlotItems(targetSlot, value, removed);
        return removed > 0;
    }

    public static bool DragDrop(
        IInventory sourceInventory,
        int sourceSlotIndex,
        DragMode dragMode,
        IInventory targetInventory,
        int targetSlotIndex,
        bool processingOnly
    )
    {
        RequireAuthority(sourceInventory);
        if (!CanDragDrop(sourceInventory, sourceSlotIndex, dragMode, targetInventory, targetSlotIndex, processingOnly))
        {
            return false;
        }

        var sourceValue = sourceInventory.GetSlotValue(sourceSlotIndex);
        var targetValue = targetInventory.GetSlotValue(targetSlotIndex);
        var sourceCount = sourceInventory.GetSlotCount(sourceSlotIndex);
        var targetCount = targetInventory.GetSlotCount(targetSlotIndex);
        var slotCapacity = targetInventory.GetSlotCapacity(targetSlotIndex, sourceValue);
        var slotProcessCapacity = targetInventory.GetSlotProcessCapacity(targetSlotIndex, sourceValue);
        if (dragMode == DragMode.SingleItem)
        {
            sourceCount = MathUtils.Min(sourceCount, 1);
        }

        var flag = false;
        if (slotProcessCapacity > 0)
        {
            // 这个可能就是拖拽衣服到角色身上，还有拖拽箭到弓上的实现
            var num6 = MathUtils.Min(sourceCount, slotProcessCapacity);
            var processCount = sourceInventory.RemoveSlotItems(sourceSlotIndex, num6);
            targetInventory.ProcessSlotItems(
                sourceInventory,
                sourceSlotIndex,
                targetSlotIndex,
                sourceValue,
                sourceCount,
                processCount,
                out var processedValue,
                out var processedCount
            );
            if (processedValue != 0 && processedCount != 0)
            {
                var count = MathUtils.Min(
                    sourceInventory.GetSlotCapacity(sourceSlotIndex, processedValue),
                    processedCount
                );
                sourceInventory.AddSlotItems(sourceSlotIndex, processedValue, count);
            }

            flag = true;
        }
        else
        {
            switch (processingOnly)
            {
                case false when
                    (targetCount == 0 ||
                     WorldContentBinding.For(sourceInventory.Project).Catalog
                         .Blocks[Terrain.ExtractContents(sourceValue)]
                         .CanAutoStack(sourceValue, targetValue)) &&
                    targetCount < slotCapacity:
                {
                    // 这个应该跟上面的移动一样吧
                    var num2 = MathUtils.Min(slotCapacity - targetCount, sourceCount);
                    if (num2 > 0)
                    {
                        var count2 = sourceInventory.RemoveSlotItems(sourceSlotIndex, num2);
                        targetInventory.AddSlotItems(targetSlotIndex, sourceValue, count2);
                        flag = true;
                    }

                    break;
                }
                case false when targetInventory.GetSlotCapacity(targetSlotIndex, sourceValue) >= sourceCount &&
                                sourceInventory.GetSlotCapacity(sourceSlotIndex, targetValue) >= targetCount &&
                                sourceInventory.GetSlotCount(sourceSlotIndex) == sourceCount:
                {
                    var count3 = targetInventory.RemoveSlotItems(targetSlotIndex, targetCount);
                    var count4 = sourceInventory.RemoveSlotItems(sourceSlotIndex, sourceCount);
                    targetInventory.AddSlotItems(targetSlotIndex, sourceValue, count4);
                    sourceInventory.AddSlotItems(sourceSlotIndex, targetValue, count3);
                    flag = true;
                    break;
                }
            }
        }

        return flag;
    }
}
