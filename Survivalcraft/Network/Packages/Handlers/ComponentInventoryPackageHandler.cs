using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ComponentInventoryPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subsystemInventories = project.FindSubsystem<SubsystemInventories>();

        IInventory? sourceInventoryObject;
        IInventory? targetInventoryObject;

        switch (PackageEventType)
        {
            case EventType.ActiveSlotChange:
                subsystemInventories?.FindInventoryById(InventoryID, inventory =>
                {
                    inventory.ActiveSlotIndex = ActiveSlot;
                    if (!isServer)
                    {
                        return;
                    }

                    Except = From;
                    netNode.QueuePackage(this);
                });
                break;
            case EventType.InventorySync:
                if (isServer)
                {
                }
                else
                {
                    //客户端接受服务器的包
                    foreach (var item in Slots)
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
            case EventType.QueryErrorInventoryInfo:
                if (isServer)
                {
                    subsystemInventories?.FindInventoryById(InventoryID, inventory =>
                    {
                        var extra = "";
                        if (inventory is ComponentCraftingTable t)
                        {
                            extra = t.Entity.ValuesDictionary.DatabaseObject.Name;
                        }

                        Log.Information($"请求错误的箱子ID[{InventoryID}]来自[{inventory.GetType().Name}][{extra}]");
                    });
                }

                break;

            case EventType.HandleMoveItem:
                if (SourceInventorySlot != null)
                {
                    sourceInventoryObject = subsystemInventories?.GetInventoryById(SourceInventorySlot.InventoryId);
                    if (TargetInventorySlot != null)
                    {
                        targetInventoryObject =
                            subsystemInventories?.GetInventoryById(TargetInventorySlot.InventoryId);
                        if (sourceInventoryObject is not null)
                            // 数据捕捉
                        {
                            if (targetInventoryObject != null)
                            {
                                InventorySlotWidget.HandleMoveItem(sourceInventoryObject,
                                    SourceInventorySlot.SlotIndex,
                                    targetInventoryObject, TargetInventorySlot.SlotIndex, TargetInventorySlot.Count);
                            }
                        }
                    }
                }

                // 服务器找不到背包？怀疑是来打服的！！！
                break;
            case EventType.HandleDragDrop:
                if (SourceInventorySlot != null)
                {
                    sourceInventoryObject = subsystemInventories?.GetInventoryById(SourceInventorySlot.InventoryId);
                    if (TargetInventorySlot != null)
                    {
                        targetInventoryObject =
                            subsystemInventories?.GetInventoryById(TargetInventorySlot.InventoryId);
                        if (sourceInventoryObject is not null)
                            // 数据捕捉
                        {
                            if (targetInventoryObject != null)
                            {
                                InventorySlotWidget.HandleDragDrop(sourceInventoryObject,
                                    SourceInventorySlot.SlotIndex,
                                    DragMode, targetInventoryObject, TargetInventorySlot.SlotIndex, ProcessingOnly);
                            }
                        }
                    }
                }

                break;
            case EventType.SetSlotsItem:
                if (isServer)
                {
                }
                else
                {
                    if (SourceInventorySlot != null)
                    {
                        sourceInventoryObject =
                            subsystemInventories?.GetInventoryById(SourceInventorySlot.InventoryId);
                        if (sourceInventoryObject is ComponentInventoryBase componentInventoryBase)
                        {
                            var slot = new ComponentInventoryBase.Slot
                            {
                                Value = SourceInventorySlot.Value,
                                Count = SourceInventorySlot.Count
                            };
                            componentInventoryBase.SetSlotValue(SourceInventorySlot.SlotIndex, slot);
                        }
                    }
                }

                break;
        }
    }
}

public sealed class ComponentInventoryPackageHandler : PackageHandlerBase<ComponentInventoryPackage>
{
    public override void Handle(ComponentInventoryPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(ComponentInventoryPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
