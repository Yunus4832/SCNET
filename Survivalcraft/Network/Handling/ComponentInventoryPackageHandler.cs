using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class ComponentInventoryPackageHandler : PackageHandlerBase<ComponentInventoryPackage>
{
    internal static bool IsValidSlot(int slot, int count) => slot >= 0 && slot < count;

    public override void Handle(ComponentInventoryPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
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

        switch (package)
        {
            case InventoryActiveSlotPackage message1:
            {
                subsystemInventories?.FindInventoryById(
                    message1.InventoryID,
                    inventory =>
                    {
                        if (!IsValidSlot(message1.ActiveSlot, inventory.SlotsCount))
                        {
                            return;
                        }

                        if (isServer &&
                            (context.Sender is null ||
                             !SubsystemInventories.CanClientAccess(inventory, context.Sender)))
                        {
                            return;
                        }

                        inventory.ActiveSlotIndex = message1.ActiveSlot;
                        if (!isServer)
                        {
                            return;
                        }

                        var observers = SubsystemInventories.GetObservers(inventory)
                            .Where(client => client.IsConnected && !ReferenceEquals(client, context.Sender))
                            .ToArray();
                        if (observers.Length > 0)
                        {
                            netNode.QueuePackage(message1, PackageAudience.To(observers));
                        }
                    }
                );
                break;
            }
            case InventorySnapshotPackage message2:
            {
                if (isServer)
                {
                }
                else
                {
                    //客户端接受服务器的包
                    foreach (var item in message2.Slots)
                    {
                        subsystemInventories?.FindInventoryById(
                            item.Key,
                            inventory =>
                            {
                                foreach (var item2 in item.Value)
                                {
                                    if (item2 is ItemSlotSnapshot itemSlot)
                                    {
                                        inventory.SetSlotValue(
                                            item2.SlotIndex,
                                            new ComponentInventoryBase.Slot
                                                { Count = itemSlot.Count, Value = itemSlot.Value }
                                        );
                                    }
                                    else if (item2 is ClothingSlotSnapshot clothes)
                                    {
                                        inventory.SetSlotValue(item2.SlotIndex, clothes.Clothes.ToList());
                                    }
                                }
                            }
                        );
                    }
                }

                break;
            }
            case InventorySyncRequestPackage message3:
            {
                if (isServer)
                {
                    var inventory = subsystemInventories?.GetInventoryById(message3.InventoryID);
                    if (inventory is not null)
                    {
                        var extra = "";
                        if (inventory is ComponentCraftingTable t)
                        {
                            extra = t.Entity.ValuesDictionary.DatabaseObject.Name;
                        }

                        Log.Debug($"请求错误的箱子ID[{message3.InventoryID}]来自[{inventory.GetType().Name}][{extra}]");
                    }
                    else
                    {
                        Log.Debug($"客户端请求的错误箱子ID[{message3.InventoryID}]在服务端也不存在");
                    }
                }

                break;
            }
            case InventoryMoveRequestPackage message4:
            {
                if (!isServer || context.Sender is null)
                {
                    break;
                }

                if (message4.Source is not null)
                {
                    sourceInventoryObject =
                        subsystemInventories?.GetInventoryById(message4.Source.InventoryId);
                    if (message4.Target is not null)
                    {
                        targetInventoryObject =
                            subsystemInventories?.GetInventoryById(message4.Target.InventoryId);
                        if (sourceInventoryObject is not null)
                            // 数据捕捉
                        {
                            if (targetInventoryObject is not null &&
                                IsValidSlot(message4.Source.SlotIndex, sourceInventoryObject.SlotsCount) &&
                                IsValidSlot(message4.Target.SlotIndex, targetInventoryObject.SlotsCount) &&
                                message4.Count > 0 &&
                                message4.Count <=
                                sourceInventoryObject.GetSlotCount(message4.Source.SlotIndex) &&
                                SubsystemInventories.CanClientAccess(sourceInventoryObject, context.Sender) &&
                                SubsystemInventories.CanClientAccess(targetInventoryObject, context.Sender))
                            {
                                InventorySlotWidget.HandleMoveItem(
                                    sourceInventoryObject,
                                    message4.Source.SlotIndex,
                                    targetInventoryObject,
                                    message4.Target.SlotIndex,
                                    message4.Count
                                );
                            }
                        }
                    }
                }

                break;
            }
            case InventoryDragDropRequestPackage message5:
            {
                if (!isServer || context.Sender is null)
                {
                    break;
                }

                if (message5.Source is not null)
                {
                    sourceInventoryObject =
                        subsystemInventories?.GetInventoryById(message5.Source.InventoryId);
                    if (message5.Target is not null)
                    {
                        targetInventoryObject =
                            subsystemInventories?.GetInventoryById(message5.Target.InventoryId);
                        if (sourceInventoryObject is not null)
                            // 数据捕捉
                        {
                            if (targetInventoryObject is not null &&
                                IsValidSlot(message5.Source.SlotIndex, sourceInventoryObject.SlotsCount) &&
                                IsValidSlot(message5.Target.SlotIndex, targetInventoryObject.SlotsCount) &&
                                ProtocolEnum.TryDecode(message5.DragMode, out DragMode dragMode) &&
                                SubsystemInventories.CanClientAccess(sourceInventoryObject, context.Sender) &&
                                SubsystemInventories.CanClientAccess(targetInventoryObject, context.Sender))
                            {
                                InventorySlotWidget.HandleDragDrop(
                                    sourceInventoryObject,
                                    message5.Source.SlotIndex,
                                    dragMode,
                                    targetInventoryObject,
                                    message5.Target.SlotIndex,
                                    message5.ProcessingOnly
                                );
                            }
                        }
                    }
                }

                break;
            }
            case InventorySetSlotPackage message6:
            {
                if (isServer)
                {
                }
                else
                {
                    if (message6.SlotIndex >= 0)
                    {
                        sourceInventoryObject =
                            subsystemInventories?.GetInventoryById(message6.InventoryId);
                        if (sourceInventoryObject is ComponentInventoryBase componentInventoryBase)
                        {
                            var slot = new ComponentInventoryBase.Slot
                            {
                                Value = message6.Value,
                                Count = message6.Count
                            };
                            componentInventoryBase.SetSlotValue(message6.SlotIndex, slot);
                        }
                    }
                }

                break;
            }
        }
    }
}
