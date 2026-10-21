using EntitySystem.Core;

using Game.Modding.Blocks;
using Game.Network.Transport;

namespace Game.Network.Handling;

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

    internal static int GetBlockContents(BuiltInBlockEditKind type)
    {
        return type switch
        {
            BuiltInBlockEditKind.MemoryBank => BuiltInBlockRegistry.GetIndex<MemoryBankBlock>(),
            BuiltInBlockEditKind.TruthTable => BuiltInBlockRegistry.GetIndex<TruthTableCircuitBlock>(),
            BuiltInBlockEditKind.AdjustableDelayGate => BuiltInBlockRegistry.GetIndex<AdjustableDelayGateBlock>(),
            BuiltInBlockEditKind.Battery => BuiltInBlockRegistry.GetIndex<BatteryBlock>(),
            BuiltInBlockEditKind.Piston => BuiltInBlockRegistry.GetIndex<PistonBlock>(),
            BuiltInBlockEditKind.Switch => BuiltInBlockRegistry.GetIndex<SwitchBlock>(),
            BuiltInBlockEditKind.Button => BuiltInBlockRegistry.GetIndex<ButtonBlock>(),
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
                if (editedInventory is null ||
                    !SubsystemInventories.CanClientAccess(editedInventory, context.Sender) ||
                    package.SlotIndex < 0 ||
                    package.SlotIndex >= editedInventory.SlotsCount)
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
                (package.ItemType == BuiltInBlockEditKind.MemoryBank && package.Data.Length > 256) ||
                (package.ItemType == BuiltInBlockEditKind.TruthTable && package.Data.Length != 16))
            {
                return;
            }
        }

        switch (package.ItemType)
        {
            case BuiltInBlockEditKind.MemoryBank:
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
                        subInventory.FindInventoryById(
                            package.InventoryId,
                            inventory =>
                            {
                                if (!package.Id.HasValue)
                                {
                                    package = package with { Id = behavior.StoreItemDataAtUniqueId(data) };
                                    ReplaceDataAtSlot(inventory, package.SlotIndex, _ => package.Id.Value);
                                }
                                else
                                {
                                    behavior.ItemsData[package.Id.Value] = data;
                                }
                            }
                        );
                    }
                    else
                    {
                        behavior.SetBlockData(package.CellFace.Point, data);
                    }
                }

                break;
            case BuiltInBlockEditKind.TruthTable:
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
                        subInventory.FindInventoryById(
                            package.InventoryId,
                            inventory =>
                            {
                                if (!package.Id.HasValue)
                                {
                                    package = package with { Id = truthBehavior.StoreItemDataAtUniqueId(truthData) };
                                    ReplaceDataAtSlot(inventory, package.SlotIndex, _ => package.Id.Value);
                                }
                                else
                                {
                                    truthBehavior.ItemsData[package.Id.Value] = truthData;
                                }
                            }
                        );
                    }
                    else
                    {
                        truthBehavior.SetBlockData(package.CellFace.Point, truthData);
                    }
                }

                break;
            case BuiltInBlockEditKind.AdjustableDelayGate:
                if (package.EditAsItem)
                {
                    subInventory.FindInventoryById(
                        package.InventoryId,
                        inventory =>
                        {
                            ReplaceDataAtSlot(
                                inventory,
                                package.SlotIndex,
                                d => AdjustableDelayGateBlock.SetDelay(d, package.Delay)
                            );
                        }
                    );
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(package.CellFace.X, package.CellFace.Y, package.CellFace.Z);
                    var newValue = Terrain.ReplaceData(
                        value,
                        AdjustableDelayGateBlock.SetDelay(Terrain.ExtractData(value), package.Delay)
                    );
                    st.ChangeCell(package.CellFace.X, package.CellFace.Y, package.CellFace.Z, newValue);
                }

                break;
            case BuiltInBlockEditKind.Battery:
                if (package.EditAsItem)
                {
                    subInventory.FindInventoryById(
                        package.InventoryId,
                        inventory =>
                        {
                            ReplaceDataAtSlot(
                                inventory,
                                package.SlotIndex,
                                d => BatteryBlock.SetVoltageLevel(d, package.Delay)
                            );
                        }
                    );
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(package.CellFace.X, package.CellFace.Y, package.CellFace.Z);
                    var newValue = Terrain.ReplaceData(
                        value,
                        BatteryBlock.SetVoltageLevel(Terrain.ExtractData(value), package.Delay)
                    );
                    st.ChangeCell(package.CellFace.X, package.CellFace.Y, package.CellFace.Z, newValue);
                }

                break;
            case BuiltInBlockEditKind.Switch:
                if (package.EditAsItem)
                {
                    subInventory.FindInventoryById(
                        package.InventoryId,
                        inventory =>
                        {
                            ReplaceDataAtSlot(
                                inventory,
                                package.SlotIndex,
                                d => SwitchBlock.SetVoltageLevel(d, package.Delay)
                            );
                        }
                    );
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(package.CellFace.X, package.CellFace.Y, package.CellFace.Z);
                    var newValue = Terrain.ReplaceData(
                        value,
                        SwitchBlock.SetVoltageLevel(Terrain.ExtractData(value), package.Delay)
                    );
                    st.ChangeCell(package.CellFace.X, package.CellFace.Y, package.CellFace.Z, newValue);
                }

                break;
            case BuiltInBlockEditKind.Button:
                if (package.EditAsItem)
                {
                    subInventory.FindInventoryById(
                        package.InventoryId,
                        inventory =>
                        {
                            ReplaceDataAtSlot(
                                inventory,
                                package.SlotIndex,
                                d => ButtonBlock.SetVoltageLevel(d, package.Delay)
                            );
                        }
                    );
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(package.CellFace.X, package.CellFace.Y, package.CellFace.Z);
                    var newValue = Terrain.ReplaceData(
                        value,
                        ButtonBlock.SetVoltageLevel(Terrain.ExtractData(value), package.Delay)
                    );
                    st.ChangeCell(package.CellFace.X, package.CellFace.Y, package.CellFace.Z, newValue);
                }

                break;
            case BuiltInBlockEditKind.Piston:
                if (package.EditAsItem)
                {
                    subInventory.FindInventoryById(
                        package.InventoryId,
                        inventory => { ReplaceDataAtSlot(inventory, package.SlotIndex, _ => package.Delay); }
                    );
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
                var definition = package.ItemType == BuiltInBlockEditKind.MemoryBank
                    ? EditableBlockPackageBuilder.Capture(
                        package.Id.Value,
                        project.FindSubsystem<SubsystemMemoryBankBlockBehavior>(true)!
                            .ItemsData[package.Id.Value]
                    )
                    : EditableBlockPackageBuilder.Capture(
                        package.Id.Value,
                        project.FindSubsystem<SubsystemTruthTableCircuitBlockBehavior>(true)!
                            .ItemsData[package.Id.Value]
                    );
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
            NetworkSender.SendToChunkObservers(
                project,
                new Point2(package.CellFace.X >> 4, package.CellFace.Z >> 4),
                package
            );
        }
    }

    private static void ReplaceDataAtSlot(IInventory inventory, int slotIndex, Func<int, int> newData)
    {
        var value = inventory.GetSlotValue(slotIndex);
        var count = inventory.GetSlotCount(slotIndex);
        inventory.RemoveSlotItems(slotIndex, count);
        var newValue = Terrain.ReplaceData(value, newData(Terrain.ExtractData(value)));
        inventory.AddSlotItems(slotIndex, newValue, count);
    }
}
