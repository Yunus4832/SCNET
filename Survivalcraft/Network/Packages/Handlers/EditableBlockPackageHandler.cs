using EntitySystem.Core;

using Game.Network.Enums;

namespace Game.Network.Packages.Handlers;

public sealed class EditableBlockPackageHandler : PackageHandlerBase<EditableBlockPackage>
{
    public static void Submit(EditableBlockPackage package)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            NetworkSender.SendToServer(package);
            return;
        }

        PackageDispatcher.Handle(package, new PackageReceiveContext(CommonLib.Net, false, null));
        if (CommonLib.WorkType == WorkType.Server && GameManager.Project is { } project)
        {
            Publish(project, package);
        }
    }

    internal static int GetBlockContents(EditableItemType type)
    {
        return type switch
        {
            EditableItemType.MemoryBank => MemoryBankBlock.Index,
            EditableItemType.TruthTable => TruthTableCircuitBlock.Index,
            EditableItemType.AdjustableDelayGate => AdjustableDelayGateBlock.Index,
            EditableItemType.Battery => BatteryBlock.Index,
            EditableItemType.Piston => PistonBlock.Index,
            EditableItemType.Switch => SwitchBlock.Index,
            EditableItemType.Button => ButtonBlock.Index,
            _ => -1
        };
    }

    public override void Handle(EditableBlockPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(EditableBlockPackage)}");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subInventory = project.FindSubsystem<SubsystemInventories>(true)!;
        IInventory? editedInventory = null;
        if (isServer)
        {
            if (context.Sender is null || package.SyncItem || package.Id.HasValue)
            {
                return;
            }

            if (package.EditAsItem)
            {
                editedInventory = subInventory.GetInventoryById(package.InventoryId);
                if (editedInventory is null || !SubsystemInventories.CanClientAccess(editedInventory, context.Sender) ||
                    package.SlotIndex < 0 || package.SlotIndex >= editedInventory.SlotsCount)
                {
                    return;
                }
            }
            else if (!project.FindSubsystem<SubsystemNetworkInterest>(true)!
                         .IsPositionRelevant(context.Sender, new Vector2(package.CellFace.X, package.CellFace.Z)))
            {
                return;
            }

            var value = package.EditAsItem
                ? editedInventory!.GetSlotValue(package.SlotIndex)
                : project.FindSubsystem<SubsystemTerrain>(true)!
                    .Terrain.GetCellValue(package.CellFace.X, package.CellFace.Y, package.CellFace.Z);
            if (Terrain.ExtractContents(value) != GetBlockContents(package.ItemType) ||
                (package.ItemType == EditableItemType.MemoryBank && package.Data.Length > 256) ||
                (package.ItemType == EditableItemType.TruthTable && package.Data.Length != 16))
            {
                return;
            }
        }

        switch (package.ItemType)
        {
            case EditableItemType.MemoryBank:
                var behavior = project.FindSubsystem<SubsystemMemoryBankBlockBehavior>();
                if (behavior is null)
                {
                    return;
                }

                var data = new MemoryBankData();
                data.Data.AddRange(package.Data);
                if (package.SyncItem)
                {
                    behavior.ItemsData[package.SlotIndex] = data;
                }
                else
                {
                    if (package.EditAsItem)
                    {
                        subInventory.FindInventoryById(package.InventoryId, inventory =>
                        {
                            if (!package.Id.HasValue)
                            {
                                package.Id = behavior.StoreItemDataAtUniqueId(data);
                                package.ReplaceDataAtSlot(inventory, package.SlotIndex, _ => package.Id.Value);
                            }
                            else
                            {
                                behavior.ItemsData[package.Id.Value] = data;
                            }
                        });
                    }
                    else
                    {
                        behavior.SetBlockData(package.CellFace.Point, data);
                    }
                }

                break;
            case EditableItemType.TruthTable:
                var truthBehavior = project.FindSubsystem<SubsystemTruthTableCircuitBlockBehavior>(true)!;
                var truthData = new TruthTableData
                {
                    Data = package.Data
                };
                if (package.SyncItem)
                {
                    truthBehavior.ItemsData[package.SlotIndex] = truthData;
                }
                else
                {
                    if (package.EditAsItem)
                    {
                        subInventory.FindInventoryById(package.InventoryId, inventory =>
                        {
                            if (!package.Id.HasValue)
                            {
                                package.Id = truthBehavior.StoreItemDataAtUniqueId(truthData);
                                package.ReplaceDataAtSlot(inventory, package.SlotIndex, _ => package.Id.Value);
                            }
                            else
                            {
                                truthBehavior.ItemsData[package.Id.Value] = truthData;
                            }
                        });
                    }
                    else
                    {
                        truthBehavior.SetBlockData(package.CellFace.Point, truthData);
                    }
                }

                break;
            case EditableItemType.AdjustableDelayGate:
                if (package.EditAsItem)
                {
                    subInventory.FindInventoryById(package.InventoryId,
                        inventory =>
                        {
                            package.ReplaceDataAtSlot(inventory, package.SlotIndex,
                                d => AdjustableDelayGateBlock.SetDelay(d, package.Delay));
                        });
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(package.CellFace.X, package.CellFace.Y, package.CellFace.Z);
                    var newValue = Terrain.ReplaceData(value,
                        AdjustableDelayGateBlock.SetDelay(Terrain.ExtractData(value), package.Delay));
                    st.ChangeCell(package.CellFace.X, package.CellFace.Y, package.CellFace.Z, newValue);
                }

                break;
            case EditableItemType.Battery:
                if (package.EditAsItem)
                {
                    subInventory.FindInventoryById(package.InventoryId,
                        inventory =>
                        {
                            package.ReplaceDataAtSlot(inventory, package.SlotIndex,
                                d => BatteryBlock.SetVoltageLevel(d, package.Delay));
                        });
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(package.CellFace.X, package.CellFace.Y, package.CellFace.Z);
                    var newValue = Terrain.ReplaceData(value,
                        BatteryBlock.SetVoltageLevel(Terrain.ExtractData(value), package.Delay));
                    st.ChangeCell(package.CellFace.X, package.CellFace.Y, package.CellFace.Z, newValue);
                }

                break;
            case EditableItemType.Switch:
                if (package.EditAsItem)
                {
                    subInventory.FindInventoryById(package.InventoryId,
                        inventory =>
                        {
                            package.ReplaceDataAtSlot(inventory, package.SlotIndex,
                                d => SwitchBlock.SetVoltageLevel(d, package.Delay));
                        });
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(package.CellFace.X, package.CellFace.Y, package.CellFace.Z);
                    var newValue = Terrain.ReplaceData(value,
                        SwitchBlock.SetVoltageLevel(Terrain.ExtractData(value), package.Delay));
                    st.ChangeCell(package.CellFace.X, package.CellFace.Y, package.CellFace.Z, newValue);
                }

                break;
            case EditableItemType.Button:
                if (package.EditAsItem)
                {
                    subInventory.FindInventoryById(package.InventoryId,
                        inventory =>
                        {
                            package.ReplaceDataAtSlot(inventory, package.SlotIndex,
                                d => ButtonBlock.SetVoltageLevel(d, package.Delay));
                        });
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(package.CellFace.X, package.CellFace.Y, package.CellFace.Z);
                    var newValue = Terrain.ReplaceData(value,
                        ButtonBlock.SetVoltageLevel(Terrain.ExtractData(value), package.Delay));
                    st.ChangeCell(package.CellFace.X, package.CellFace.Y, package.CellFace.Z, newValue);
                }

                break;
            case EditableItemType.Piston:
                if (package.EditAsItem)
                {
                    subInventory.FindInventoryById(package.InventoryId,
                        inventory => { package.ReplaceDataAtSlot(inventory, package.SlotIndex, _ => package.Delay); });
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(package.CellFace.X, package.CellFace.Y, package.CellFace.Z);
                    var newValue = Terrain.ReplaceData(value, package.Delay);
                    st.ChangeCell(package.CellFace.X, package.CellFace.Y, package.CellFace.Z, newValue);
                }

                break;
        }

        if (isServer)
        {
            Publish(project, package);
        }
    }

    private static void Publish(Project project, EditableBlockPackage package)
    {
        var netNode = CommonLib.Net;
        var editedInventory = project.FindSubsystem<SubsystemInventories>(true)!
            .GetInventoryById(package.InventoryId);
        if (package.EditAsItem)
        {
            if (package.Id.HasValue)
            {
                var definition = package.ItemType == EditableItemType.MemoryBank
                    ? new EditableBlockPackage(package.Id.Value,
                        project.FindSubsystem<SubsystemMemoryBankBlockBehavior>(true)!
                            .ItemsData[package.Id.Value])
                    : new EditableBlockPackage(package.Id.Value,
                        project.FindSubsystem<SubsystemTruthTableCircuitBlockBehavior>(true)!
                            .ItemsData[package.Id.Value]);
                // 物品数据 ID 是世界级字典引用，不是库存操作；后续观察者也必须能解析。
                NetworkSender.SendGlobal(definition);
            }
            else
            {
                var observers = SubsystemInventories.GetObservers(editedInventory!).ToArray();
                if (observers.Length > 0)
                {
                    netNode.QueuePackage(package, PackageAudience.To(observers));
                }
            }
        }
        else
        {
            NetworkSender.SendToChunkObservers(project,
                new Point2(package.CellFace.X >> 4, package.CellFace.Z >> 4), package);
        }
    }
}
