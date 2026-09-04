namespace Game.Network.Packages.Handlers;

public sealed class ComponentInventoryPackageHandler : PackageHandlerBase<ComponentInventoryPackage>
{
    public override void Handle(ComponentInventoryPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(ComponentInventoryPackage)}");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subsystemInventories = project.FindSubsystem<SubsystemInventories>();

        IInventory? sourceInventoryObject;
        IInventory? targetInventoryObject;

        switch (package.PackageEventType)
        {
            case ComponentInventoryPackage.EventType.ActiveSlotChange:
                subsystemInventories?.FindInventoryById(package.InventoryID, inventory =>
                {
                    inventory.ActiveSlotIndex = package.ActiveSlot;
                    if (!isServer)
                    {
                        return;
                    }

                    package.Except = package.From;
                    netNode.QueuePackage(package);
                });
                break;
            case ComponentInventoryPackage.EventType.InventorySync:
                if (isServer)
                {
                }
                else
                {
                    //客户端接受服务器的包
                    foreach (var item in package.Slots)
                    {
                        subsystemInventories?.FindInventoryById(item.Key, inventory =>
                        {
                            foreach (var item2 in item.Value)
                            {
                                if (item2.Type == 0)
                                {
                                    if (item2.SlotItem != null)
                                    {
                                        inventory.SetSlotValue(item2.SlotIndex, item2.SlotItem);
                                    }
                                }
                                else
                                {
                                    inventory.SetSlotValue(item2.SlotIndex, item2.ClothingList);
                                }
                            }
                        });
                    }
                }

                break;
            case ComponentInventoryPackage.EventType.QueryErrorInventoryInfo:
                if (isServer)
                {
                    var inventory = subsystemInventories?.GetInventoryById(package.InventoryID);
                    if (inventory is not null)
                    {
                        var extra = "";
                        if (inventory is ComponentCraftingTable t)
                        {
                            extra = t.Entity.ValuesDictionary.DatabaseObject.Name;
                        }

                        Log.Debug($"请求错误的箱子ID[{package.InventoryID}]来自[{inventory.GetType().Name}][{extra}]");
                    }
                    else
                    {
                        Log.Debug($"客户端请求的错误箱子ID[{package.InventoryID}]在服务端也不存在");
                    }
                }

                break;

            case ComponentInventoryPackage.EventType.HandleMoveItem:
                if (package.SourceInventorySlot != null)
                {
                    sourceInventoryObject =
                        subsystemInventories?.GetInventoryById(package.SourceInventorySlot.InventoryId);
                    if (package.TargetInventorySlot != null)
                    {
                        targetInventoryObject =
                            subsystemInventories?.GetInventoryById(package.TargetInventorySlot.InventoryId);
                        if (sourceInventoryObject is not null)
                        // 数据捕捉
                        {
                            if (targetInventoryObject != null)
                            {
                                InventorySlotWidget.HandleMoveItem(sourceInventoryObject,
                                    package.SourceInventorySlot.SlotIndex,
                                    targetInventoryObject, package.TargetInventorySlot.SlotIndex,
                                    package.TargetInventorySlot.Count);
                            }
                        }
                    }
                }

                // 服务器找不到背包？怀疑是来打服的！！！
                break;
            case ComponentInventoryPackage.EventType.HandleDragDrop:
                if (package.SourceInventorySlot != null)
                {
                    sourceInventoryObject =
                        subsystemInventories?.GetInventoryById(package.SourceInventorySlot.InventoryId);
                    if (package.TargetInventorySlot != null)
                    {
                        targetInventoryObject =
                            subsystemInventories?.GetInventoryById(package.TargetInventorySlot.InventoryId);
                        if (sourceInventoryObject is not null)
                        // 数据捕捉
                        {
                            if (targetInventoryObject != null)
                            {
                                InventorySlotWidget.HandleDragDrop(sourceInventoryObject,
                                    package.SourceInventorySlot.SlotIndex,
                                    package.DragMode, targetInventoryObject, package.TargetInventorySlot.SlotIndex,
                                    package.ProcessingOnly);
                            }
                        }
                    }
                }

                break;
            case ComponentInventoryPackage.EventType.SetSlotsItem:
                if (isServer)
                {
                }
                else
                {
                    if (package.SourceInventorySlot != null)
                    {
                        sourceInventoryObject =
                            subsystemInventories?.GetInventoryById(package.SourceInventorySlot.InventoryId);
                        if (sourceInventoryObject is ComponentInventoryBase componentInventoryBase)
                        {
                            var slot = new ComponentInventoryBase.Slot
                            {
                                Value = package.SourceInventorySlot.Value,
                                Count = package.SourceInventorySlot.Count
                            };
                            componentInventoryBase.SetSlotValue(package.SourceInventorySlot.SlotIndex, slot);
                        }
                    }
                }

                break;
        }
    }
}
