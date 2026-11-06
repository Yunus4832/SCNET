using EntitySystem.Core;

using Game.ElectricElements;

namespace Game.Network.Handling;

public sealed class EditableBlockPackageHandler : PackageHandlerBase<EditableBlockPackage>
{
    public static void Submit(Project project, EditableBlockPackage package)
    {
        NetworkSender.SendToServer(WorldRuntimeContext.For(project), package);
    }

    public static int GetBlockContents(BuiltInBlockEditKind type)
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
        if (context.Node is not { } node || context.Project is not { } project)
        {
            return;
        }

        Apply(package, project, node, context.Sender, context.IsServer);
    }

    private static void Apply(
        EditableBlockPackage package,
        Project project,
        NetNode netNode,
        Client? sender,
        bool isServer
    )
    {
        var inventoryTarget = package.Target as EditableInventorySlotTarget;
        var editAsItem = inventoryTarget is not null;
        var syncItem = package.Target is EditableItemDefinitionTarget;
        var inventoryId = inventoryTarget?.InventoryId ?? 0;
        var slotIndex = inventoryTarget?.SlotIndex ??
                        (package.Target as EditableItemDefinitionTarget)?.DefinitionId ?? 0;
        var cellFace = (package.Target as EditableWorldBlockTarget)?.CellFace ?? default;
        var definitionId = (package as EditableBlockDataPackage)?.DefinitionId;
        var dataBytes = (package as EditableBlockDataPackage)?.Data ?? [];
        var editedValue = (package as EditableBlockValuePackage)?.Value ?? 0;
        var subInventory = project.FindSubsystem<SubsystemInventories>(true)!;
        IInventory? editedInventory = null;
        if (isServer)
        {
            if (sender is null || syncItem || definitionId.HasValue)
            {
                return;
            }

            if (editAsItem)
            {
                editedInventory = subInventory.GetInventoryById(inventoryId);
                if (editedInventory is null ||
                    !SubsystemInventories.CanClientAccess(editedInventory, sender) ||
                    slotIndex < 0 ||
                    slotIndex >= editedInventory.SlotsCount)
                {
                    return;
                }
            }
            else if (!project.FindSubsystem<SubsystemNetworkInterest>(true)!
                         .IsPositionRelevant(sender, new Vector2(cellFace.X, cellFace.Z)))
            {
                return;
            }

            var value = editAsItem
                ? editedInventory!.GetSlotValue(slotIndex)
                : project.FindSubsystem<SubsystemTerrain>(true)!
                    .Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
            if (Terrain.ExtractContents(value) != GetBlockContents(package.ItemType) ||
                (package.ItemType == BuiltInBlockEditKind.MemoryBank && dataBytes.Length > 256) ||
                (package.ItemType == BuiltInBlockEditKind.TruthTable && dataBytes.Length != 16))
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
                data.Data.AddRange(dataBytes);
                if (syncItem)
                {
                    behavior.ItemsData[slotIndex] = data;
                }
                else
                {
                    if (editAsItem)
                    {
                        subInventory.FindInventoryById(
                            inventoryId,
                            inventory =>
                            {
                                if (!definitionId.HasValue)
                                {
                                    definitionId = behavior.StoreItemDataAtUniqueId(data);
                                    package = ((EditableBlockDataPackage)package) with { DefinitionId = definitionId };
                                    ReplaceDataAtSlot(inventory, slotIndex, _ => definitionId.Value);
                                }
                                else
                                {
                                    behavior.ItemsData[definitionId.Value] = data;
                                }
                            }
                        );
                    }
                    else
                    {
                        behavior.SetBlockData(cellFace.Point, data);
                    }
                }

                break;
            case BuiltInBlockEditKind.TruthTable:
                var truthBehavior = project.FindSubsystem<SubsystemTruthTableCircuitBlockBehavior>(true)!;
                var truthData = new TruthTableData
                {
                    Data = dataBytes
                };
                if (syncItem)
                {
                    truthBehavior.ItemsData[slotIndex] = truthData;
                }
                else
                {
                    if (editAsItem)
                    {
                        subInventory.FindInventoryById(
                            inventoryId,
                            inventory =>
                            {
                                if (!definitionId.HasValue)
                                {
                                    definitionId = truthBehavior.StoreItemDataAtUniqueId(truthData);
                                    package = ((EditableBlockDataPackage)package) with { DefinitionId = definitionId };
                                    ReplaceDataAtSlot(inventory, slotIndex, _ => definitionId.Value);
                                }
                                else
                                {
                                    truthBehavior.ItemsData[definitionId.Value] = truthData;
                                }
                            }
                        );
                    }
                    else
                    {
                        truthBehavior.SetBlockData(cellFace.Point, truthData);
                    }
                }

                break;
            case BuiltInBlockEditKind.AdjustableDelayGate:
                if (editAsItem)
                {
                    subInventory.FindInventoryById(
                        inventoryId,
                        inventory =>
                        {
                            ReplaceDataAtSlot(
                                inventory,
                                slotIndex,
                                d => AdjustableDelayGateBlock.SetDelay(d, editedValue)
                            );
                        }
                    );
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
                    var newValue = Terrain.ReplaceData(
                        value,
                        AdjustableDelayGateBlock.SetDelay(Terrain.ExtractData(value), editedValue)
                    );
                    st.ChangeCell(cellFace.X, cellFace.Y, cellFace.Z, newValue);
                }

                break;
            case BuiltInBlockEditKind.Battery:
                if (editAsItem)
                {
                    subInventory.FindInventoryById(
                        inventoryId,
                        inventory =>
                        {
                            ReplaceDataAtSlot(
                                inventory,
                                slotIndex,
                                d => BatteryBlock.SetVoltageLevel(d, editedValue)
                            );
                        }
                    );
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
                    var newValue = Terrain.ReplaceData(
                        value,
                        BatteryBlock.SetVoltageLevel(Terrain.ExtractData(value), editedValue)
                    );
                    st.ChangeCell(cellFace.X, cellFace.Y, cellFace.Z, newValue);
                }

                break;
            case BuiltInBlockEditKind.Switch:
                if (editAsItem)
                {
                    subInventory.FindInventoryById(
                        inventoryId,
                        inventory =>
                        {
                            ReplaceDataAtSlot(
                                inventory,
                                slotIndex,
                                d => SwitchBlock.SetVoltageLevel(d, editedValue)
                            );
                        }
                    );
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
                    var newValue = Terrain.ReplaceData(
                        value,
                        SwitchBlock.SetVoltageLevel(Terrain.ExtractData(value), editedValue)
                    );
                    st.ChangeCell(cellFace.X, cellFace.Y, cellFace.Z, newValue);
                }

                break;
            case BuiltInBlockEditKind.Button:
                if (editAsItem)
                {
                    subInventory.FindInventoryById(
                        inventoryId,
                        inventory =>
                        {
                            ReplaceDataAtSlot(
                                inventory,
                                slotIndex,
                                d => ButtonBlock.SetVoltageLevel(d, editedValue)
                            );
                        }
                    );
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
                    var newValue = Terrain.ReplaceData(
                        value,
                        ButtonBlock.SetVoltageLevel(Terrain.ExtractData(value), editedValue)
                    );
                    st.ChangeCell(cellFace.X, cellFace.Y, cellFace.Z, newValue);
                }

                break;
            case BuiltInBlockEditKind.Piston:
                if (editAsItem)
                {
                    subInventory.FindInventoryById(
                        inventoryId,
                        inventory => { ReplaceDataAtSlot(inventory, slotIndex, _ => editedValue); }
                    );
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
                    var newValue = Terrain.ReplaceData(value, editedValue);
                    st.ChangeCell(cellFace.X, cellFace.Y, cellFace.Z, newValue);
                }

                break;
        }

        if (isServer)
        {
            Publish(project, package, netNode);
        }
    }

    private static void Publish(Project project, EditableBlockPackage package, NetNode netNode)
    {
        var inventoryTarget = package.Target as EditableInventorySlotTarget;
        var editAsItem = inventoryTarget is not null;
        var syncItem = package.Target is EditableItemDefinitionTarget;
        var inventoryId = inventoryTarget?.InventoryId ?? 0;
        var slotIndex = inventoryTarget?.SlotIndex ??
                        (package.Target as EditableItemDefinitionTarget)?.DefinitionId ?? 0;
        var cellFace = (package.Target as EditableWorldBlockTarget)?.CellFace ?? default;
        var definitionId = (package as EditableBlockDataPackage)?.DefinitionId;

        var editedInventory = project.FindSubsystem<SubsystemInventories>(true)!
            .GetInventoryById(inventoryId);
        if (editAsItem)
        {
            if (definitionId.HasValue)
            {
                var definition = package.ItemType == BuiltInBlockEditKind.MemoryBank
                    ? EditableBlockPackageBuilder.Capture(
                        definitionId.Value,
                        project.FindSubsystem<SubsystemMemoryBankBlockBehavior>(true)!
                            .ItemsData[definitionId.Value]
                    )
                    : EditableBlockPackageBuilder.Capture(
                        definitionId.Value,
                        project.FindSubsystem<SubsystemTruthTableCircuitBlockBehavior>(true)!
                            .ItemsData[definitionId.Value]
                    );
                // 物品数据 ID 是世界级字典引用，不是库存操作；后续观察者也必须能解析。
                NetworkSender.SendGlobal(project, definition);
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
                new Point2(cellFace.X >> 4, cellFace.Z >> 4),
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
