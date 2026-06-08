using Game.Network.Enums;

namespace Game.Network.Packages.Handlers;

public sealed class EditableBlockPackageHandler : PackageHandlerBase<EditableBlockPackage>
{
    public override void Handle(EditableBlockPackage package, NetNode? netNode, bool isServer)
    {
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
            netNode.QueuePackage(package);
        }
    }
}
