using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class EditableBlockPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subInventory = project.FindSubsystem<SubsystemInventories>(true)!;
        switch (ItemType)
        {
            case EditableItemType.MemoryBank:
                var behavior = project.FindSubsystem<SubsystemMemoryBankBlockBehavior>();
                if (behavior is null)
                {
                    return;
                }

                var data = new MemoryBankData();
                data.Data.AddRange(Data);
                if (SyncItem)
                {
                    behavior.ItemsData[SlotIndex] = data;
                }
                else
                {
                    if (EditAsItem)
                    {
                        subInventory.FindInventoryById(InventoryId, inventory =>
                        {
                            if (!Id.HasValue)
                            {
                                Id = behavior.StoreItemDataAtUniqueId(data);
                                ReplaceDataAtSlot(inventory, SlotIndex, _ => Id.Value);
                            }
                            else
                            {
                                behavior.ItemsData[Id.Value] = data;
                            }
                        });
                    }
                    else
                    {
                        behavior.SetBlockData(CellFace.Point, data);
                    }
                }

                break;
            case EditableItemType.TruthTable:
                var truthBehavior = project.FindSubsystem<SubsystemTruthTableCircuitBlockBehavior>(true)!;
                var truthData = new TruthTableData
                {
                    Data = Data
                };
                if (SyncItem)
                {
                    truthBehavior.ItemsData[SlotIndex] = truthData;
                }
                else
                {
                    if (EditAsItem)
                    {
                        subInventory.FindInventoryById(InventoryId, inventory =>
                        {
                            if (!Id.HasValue)
                            {
                                Id = truthBehavior.StoreItemDataAtUniqueId(truthData);
                                ReplaceDataAtSlot(inventory, SlotIndex, _ => Id.Value);
                            }
                            else
                            {
                                truthBehavior.ItemsData[Id.Value] = truthData;
                            }
                        });
                    }
                    else
                    {
                        truthBehavior.SetBlockData(CellFace.Point, truthData);
                    }
                }

                break;
            case EditableItemType.AdjustableDelayGate:
                if (EditAsItem)
                {
                    subInventory.FindInventoryById(InventoryId,
                        inventory =>
                        {
                            ReplaceDataAtSlot(inventory, SlotIndex,
                                d => AdjustableDelayGateBlock.SetDelay(d, Delay));
                        });
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(CellFace.X, CellFace.Y, CellFace.Z);
                    var newValue = Terrain.ReplaceData(value,
                        AdjustableDelayGateBlock.SetDelay(Terrain.ExtractData(value), Delay));
                    st.ChangeCell(CellFace.X, CellFace.Y, CellFace.Z, newValue);
                }

                break;
            case EditableItemType.Battery:
                if (EditAsItem)
                {
                    subInventory.FindInventoryById(InventoryId,
                        inventory =>
                        {
                            ReplaceDataAtSlot(inventory, SlotIndex, d => BatteryBlock.SetVoltageLevel(d, Delay));
                        });
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(CellFace.X, CellFace.Y, CellFace.Z);
                    var newValue = Terrain.ReplaceData(value,
                        BatteryBlock.SetVoltageLevel(Terrain.ExtractData(value), Delay));
                    st.ChangeCell(CellFace.X, CellFace.Y, CellFace.Z, newValue);
                }

                break;
            case EditableItemType.Switch:
                if (EditAsItem)
                {
                    subInventory.FindInventoryById(InventoryId,
                        inventory =>
                        {
                            ReplaceDataAtSlot(inventory, SlotIndex, d => SwitchBlock.SetVoltageLevel(d, Delay));
                        });
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(CellFace.X, CellFace.Y, CellFace.Z);
                    var newValue = Terrain.ReplaceData(value,
                        SwitchBlock.SetVoltageLevel(Terrain.ExtractData(value), Delay));
                    st.ChangeCell(CellFace.X, CellFace.Y, CellFace.Z, newValue);
                }

                break;
            case EditableItemType.Button:
                if (EditAsItem)
                {
                    subInventory.FindInventoryById(InventoryId,
                        inventory =>
                        {
                            ReplaceDataAtSlot(inventory, SlotIndex, d => ButtonBlock.SetVoltageLevel(d, Delay));
                        });
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(CellFace.X, CellFace.Y, CellFace.Z);
                    var newValue = Terrain.ReplaceData(value,
                        ButtonBlock.SetVoltageLevel(Terrain.ExtractData(value), Delay));
                    st.ChangeCell(CellFace.X, CellFace.Y, CellFace.Z, newValue);
                }

                break;
            case EditableItemType.Piston:
                if (EditAsItem)
                {
                    subInventory.FindInventoryById(InventoryId,
                        inventory => { ReplaceDataAtSlot(inventory, SlotIndex, _ => Delay); });
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(CellFace.X, CellFace.Y, CellFace.Z);
                    var newValue = Terrain.ReplaceData(value, Delay);
                    st.ChangeCell(CellFace.X, CellFace.Y, CellFace.Z, newValue);
                }

                break;
        }

        if (isServer)
        {
            netNode.QueuePackage(this);
        }
    }
}

public sealed class EditableBlockPackageHandler : PackageHandlerBase<EditableBlockPackage>
{
    public override void Handle(EditableBlockPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(EditableBlockPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
