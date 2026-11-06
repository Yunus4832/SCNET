using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Modding.Blocks;
using Game.Network.Handling;
using Game.Subsystems.Circuits;

namespace Game.Subsystems;

public sealed class SubsystemCircuitEditPresentation : Subsystem
{
    private SubsystemBatteryBlockBehavior _battery = null!;

    private SubsystemPistonBlockBehavior _piston = null!;

    private SubsystemAdjustableDelayGateBlockBehavior _delay = null!;

    private SubsystemMemoryBankBlockBehavior _memory = null!;

    private SubsystemTruthTableCircuitBlockBehavior _truthTable = null!;

    private SubsystemTerrain _terrain = null!;

    private bool _disposed;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _battery = Project.FindSubsystem<SubsystemBatteryBlockBehavior>(true)!;
        _piston = Project.FindSubsystem<SubsystemPistonBlockBehavior>(true)!;
        _delay = Project.FindSubsystem<SubsystemAdjustableDelayGateBlockBehavior>(true)!;
        _memory = Project.FindSubsystem<SubsystemMemoryBankBlockBehavior>(true)!;
        _truthTable = Project.FindSubsystem<SubsystemTruthTableCircuitBlockBehavior>(true)!;
        _terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _battery.InventoryEditRequested += OnInventoryEdit;
        _battery.BlockEditRequested += OnBlockEdit;
        _piston.InventoryEditRequested += OnInventoryEdit;
        _piston.BlockEditRequested += OnBlockEdit;
        _delay.InventoryEditRequested += OnInventoryEdit;
        _delay.BlockEditRequested += OnBlockEdit;
        _memory.InventoryEditRequested += OnInventoryEdit;
        _memory.BlockEditRequested += OnBlockEdit;
        _truthTable.InventoryEditRequested += OnInventoryEdit;
        _truthTable.BlockEditRequested += OnBlockEdit;
    }

    private static bool CanPresentCharacter(Project project, ComponentCharacter character)
    {
        return WorldRuntimeContext.IsProjectActive(project) &&
               ReferenceEquals(character.Project, project) &&
               character.IsLocallyControlled;
    }

    internal static MemoryBankData CreateMemoryBankWorkingCopy(MemoryBankData? source)
    {
        return source is null ? new MemoryBankData() : (MemoryBankData)source.Copy();
    }

    internal static TruthTableData CreateTruthTableWorkingCopy(TruthTableData? source)
    {
        return source is null ? new TruthTableData() : (TruthTableData)source.Copy();
    }

    internal static bool CanEditInventory(Project project, CircuitInventoryEditRequest request)
    {
        return CanPresentCharacter(project, request.Character) &&
               ReferenceEquals(request.Inventory.Project, project) &&
               request.SlotIndex >= 0 &&
               request.SlotIndex < request.Inventory.SlotsCount &&
               request.Inventory.GetSlotCount(request.SlotIndex) > 0 &&
               request.Inventory.GetSlotValue(request.SlotIndex) == request.OriginalValue &&
               Terrain.ExtractContents(request.OriginalValue) ==
               EditableBlockPackageHandler.GetBlockContents(request.Kind);
    }

    internal static bool CanEditBlock(Project project, SubsystemTerrain terrain, CircuitBlockEditRequest request)
    {
        return CanPresentCharacter(project, request.Character) &&
               terrain.Terrain.GetCellContents(request.Point.X, request.Point.Y, request.Point.Z) ==
               EditableBlockPackageHandler.GetBlockContents(request.Kind) &&
               Terrain.ExtractContents(request.OriginalValue) ==
               EditableBlockPackageHandler.GetBlockContents(request.Kind);
    }

    private void OnInventoryEdit(CircuitInventoryEditRequest request)
    {
        if (_disposed || !CanEditInventory(Project, request)) { return; }

        switch (request.Kind)
        {
            case BuiltInBlockEditKind.Battery:
                EditBatteryInventory(request.Inventory, request.SlotIndex, request.Character);
                break;
            case BuiltInBlockEditKind.Piston:
                EditPistonInventory(request.Inventory, request.SlotIndex, request.Character);
                break;
            case BuiltInBlockEditKind.AdjustableDelayGate:
                EditAdjustableDelayGateInventory(request.Inventory, request.SlotIndex, request.Character);
                break;
            case BuiltInBlockEditKind.MemoryBank:
                EditMemoryBankInventory(request.Inventory, request.SlotIndex, request.Character);
                break;
            case BuiltInBlockEditKind.TruthTable:
                EditTruthTableCircuitInventory(request.Inventory, request.SlotIndex, request.Character);
                break;
        }
    }

    private void OnBlockEdit(CircuitBlockEditRequest request)
    {
        if (_disposed || !CanEditBlock(Project, _terrain, request)) { return; }

        var point = request.Point;
        switch (request.Kind)
        {
            case BuiltInBlockEditKind.Battery:
                EditBatteryBlock(point.X, point.Y, point.Z, request.OriginalValue, request.Character);
                break;
            case BuiltInBlockEditKind.Piston:
                EditPistonBlock(point.X, point.Y, point.Z, request.OriginalValue, request.Character);
                break;
            case BuiltInBlockEditKind.AdjustableDelayGate:
                EditAdjustableDelayGateBlock(point.X, point.Y, point.Z, request.OriginalValue, request.Character);
                break;
            case BuiltInBlockEditKind.MemoryBank:
                EditMemoryBankBlock(point.X, point.Y, point.Z, request.OriginalValue, request.Character);
                break;
            case BuiltInBlockEditKind.TruthTable:
                EditTruthTableCircuitBlock(point.X, point.Y, point.Z, request.OriginalValue, request.Character);
                break;
        }
    }

    private void SubmitInventory(
        ComponentCharacter character,
        IInventory inventory,
        int slotIndex,
        int originalValue,
        EditableBlockPackage package
    )
    {
        if (!_disposed &&
            CanEditInventory(
                Project,
                new CircuitInventoryEditRequest(character, inventory, slotIndex, originalValue, package.ItemType)
            ))
        {
            EditableBlockPackageHandler.Submit(Project, package);
        }
    }

    private void SubmitBlock(
        ComponentCharacter character,
        Point3 point,
        int originalValue,
        EditableBlockPackage package
    )
    {
        if (!_disposed &&
            CanEditBlock(
                Project,
                _terrain,
                new CircuitBlockEditRequest(character, point, originalValue, package.ItemType)
            ))
        {
            EditableBlockPackageHandler.Submit(Project, package);
        }
    }

    private void CloseEditor(ComponentCharacter character)
    {
        if (!_disposed && CanPresentCharacter(Project, character))
        {
            character.Entity.FindComponent<ComponentGui>(true)!.ModalPanelWidget = null;
        }
    }

    private void EditBatteryInventory(IInventory inventory, int slotIndex, ComponentCharacter componentCharacter)
    {
        var value = inventory.GetSlotValue(slotIndex);
        inventory.GetSlotCount(slotIndex);
        var data = Terrain.ExtractData(value);
        var voltageLevel = BatteryBlock.GetVoltageLevel(data);
        DialogsManager.ShowDialog(
            componentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GuiWidget,
            new EditBatteryDialog(
                voltageLevel,
                delegate(int newVoltageLevel)
                {
                    var data2 = BatteryBlock.SetVoltageLevel(data, newVoltageLevel);
                    var num = Terrain.ReplaceData(value, data2);
                    if (num == value)
                    {
                        return;
                    }

                    var p = new EditableBlockValuePackage
                    {
                        ItemType = BuiltInBlockEditKind.Battery,
                        Target = new EditableInventorySlotTarget(inventory.Id, slotIndex), Value = newVoltageLevel
                    };
                    SubmitInventory(componentCharacter, inventory, slotIndex, value, p);
                }
            )
        );
    }

    private void EditBatteryBlock(int x, int y, int z, int value, ComponentCharacter componentCharacter)
    {
        var data = Terrain.ExtractData(value);
        var voltageLevel = BatteryBlock.GetVoltageLevel(data);
        DialogsManager.ShowDialog(
            componentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GuiWidget,
            new EditBatteryDialog(
                voltageLevel,
                delegate(int newVoltageLevel)
                {
                    var num = BatteryBlock.SetVoltageLevel(data, newVoltageLevel);
                    if (num == data)
                    {
                        return;
                    }

                    const int face = 4;
                    var cell = new CellFace(x, y, z, face);
                    var p = new EditableBlockValuePackage
                    {
                        ItemType = BuiltInBlockEditKind.Battery, Target = new EditableWorldBlockTarget(cell),
                        Value = newVoltageLevel
                    };
                    SubmitBlock(componentCharacter, new Point3(x, y, z), value, p);
                }
            )
        );
    }

    private void EditPistonInventory(IInventory inventory, int slotIndex, ComponentCharacter componentCharacter)
    {
        var value = inventory.GetSlotValue(slotIndex);
        inventory.GetSlotCount(slotIndex);
        var data = Terrain.ExtractData(value);
        DialogsManager.ShowDialog(
            componentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GuiWidget,
            new EditPistonDialog(
                Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog,
                data,
                delegate(int newData)
                {
                    var num = Terrain.ReplaceData(value, newData);
                    if (num == value)
                    {
                        return;
                    }

                    var p = new EditableBlockValuePackage
                    {
                        ItemType = BuiltInBlockEditKind.Piston,
                        Target = new EditableInventorySlotTarget(inventory.Id, slotIndex), Value = newData
                    };
                    SubmitInventory(componentCharacter, inventory, slotIndex, value, p);
                }
            )
        );
    }

    private void EditPistonBlock(int x, int y, int z, int value, ComponentCharacter componentCharacter)
    {
        var contents = Terrain.ExtractContents(value);
        var data = Terrain.ExtractData(value);
        DialogsManager.ShowDialog(
            componentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GuiWidget,
            new EditPistonDialog(
                Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog,
                data,
                delegate(int newData)
                {
                    if (newData == data || _terrain.Terrain.GetCellContents(x, y, z) != contents)
                    {
                        return;
                    }

                    var cell = new CellFace(x, y, z, 0);
                    var p = new EditableBlockValuePackage
                    {
                        ItemType = BuiltInBlockEditKind.Piston, Target = new EditableWorldBlockTarget(cell),
                        Value = newData
                    };
                    SubmitBlock(componentCharacter, new Point3(x, y, z), value, p);
                }
            )
        );
    }

    private void EditAdjustableDelayGateInventory(
        IInventory inventory,
        int slotIndex,
        ComponentCharacter componentCharacter
    )
    {
        var value = inventory.GetSlotValue(slotIndex);
        inventory.GetSlotCount(slotIndex);
        var data = Terrain.ExtractData(value);
        var delay = AdjustableDelayGateBlock.GetDelay(data);
        DialogsManager.ShowDialog(
            componentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GuiWidget,
            new EditAdjustableDelayGateDialog(
                delay,
                delegate(int newDelay)
                {
                    var data2 = AdjustableDelayGateBlock.SetDelay(data, newDelay);
                    var num = Terrain.ReplaceData(value, data2);
                    if (num != value)
                    {
                        var p = new EditableBlockValuePackage
                        {
                            ItemType = BuiltInBlockEditKind.AdjustableDelayGate,
                            Target = new EditableInventorySlotTarget(inventory.Id, slotIndex), Value = newDelay
                        };
                        SubmitInventory(componentCharacter, inventory, slotIndex, value, p);
                    }
                }
            )
        );
    }

    private void EditAdjustableDelayGateBlock(int x, int y, int z, int value, ComponentCharacter componentCharacter)
    {
        var data = Terrain.ExtractData(value);
        var delay = AdjustableDelayGateBlock.GetDelay(data);
        DialogsManager.ShowDialog(
            componentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GuiWidget,
            new EditAdjustableDelayGateDialog(
                delay,
                delegate(int newDelay)
                {
                    var num = AdjustableDelayGateBlock.SetDelay(data, newDelay);
                    if (num == data)
                    {
                        return;
                    }

                    var face =
                        ((AdjustableDelayGateBlock)WorldContentBinding.For(Project).Catalog
                            .Blocks[BuiltInBlockRegistry.GetIndex<AdjustableDelayGateBlock>()]).GetFace(value);
                    var cell = new CellFace(x, y, z, face);
                    var p = new EditableBlockValuePackage
                    {
                        ItemType = BuiltInBlockEditKind.AdjustableDelayGate,
                        Target = new EditableWorldBlockTarget(cell), Value = newDelay
                    };
                    SubmitBlock(componentCharacter, new Point3(x, y, z), value, p);
                }
            )
        );
    }

    private void EditMemoryBankInventory(IInventory inventory, int slotIndex, ComponentCharacter componentCharacter)
    {
        var value = inventory.GetSlotValue(slotIndex);
        inventory.GetSlotCount(slotIndex);
        var id = Terrain.ExtractData(value);
        var memoryBankData = _memory.GetItemData(id);
        memoryBankData = CreateMemoryBankWorkingCopy(memoryBankData);
        var editor = new MemoryBankEditorWidget(
            memoryBankData,
            saved =>
            {
                if (!saved)
                {
                    return;
                }

                var p = EditableBlockPackageBuilder.Capture(default, true, inventory.Id, slotIndex, memoryBankData);
                SubmitInventory(componentCharacter, inventory, slotIndex, value, p);
            },
            () => CloseEditor(componentCharacter)
        );
        componentCharacter.Entity.FindComponent<ComponentGui>(true)!.ModalPanelWidget = editor;
    }

    private void EditMemoryBankBlock(int x, int y, int z, int value, ComponentCharacter componentCharacter)
    {
        var source = _memory.GetBlockData(new Point3(x, y, z));
        var memoryBankData = CreateMemoryBankWorkingCopy(source);
        var editor = new MemoryBankEditorWidget(
            memoryBankData,
            saved =>
            {
                if (!saved)
                {
                    return;
                }

                var face = ((MemoryBankBlock)WorldContentBinding.For(Project).Catalog.Blocks[186])
                    .GetFace(value);
                var cell = new CellFace(x, y, z, face);
                var p = EditableBlockPackageBuilder.Capture(cell, false, 0, 0, memoryBankData);
                SubmitBlock(componentCharacter, new Point3(x, y, z), value, p);
            },
            () => CloseEditor(componentCharacter)
        );
        componentCharacter.Entity.FindComponent<ComponentGui>(true)!.ModalPanelWidget = editor;
    }

    private void EditTruthTableCircuitInventory(
        IInventory inventory,
        int slotIndex,
        ComponentCharacter componentCharacter
    )
    {
        var value = inventory.GetSlotValue(slotIndex);
        inventory.GetSlotCount(slotIndex);
        var id = Terrain.ExtractData(value);
        var truthTableData = _truthTable.GetItemData(id);
        truthTableData = CreateTruthTableWorkingCopy(truthTableData);
        var editor = new TruthTableEditorWidget(
            truthTableData,
            saved =>
            {
                if (!saved)
                {
                    return;
                }

                var p = EditableBlockPackageBuilder.Capture(default, true, inventory.Id, slotIndex, truthTableData);
                SubmitInventory(componentCharacter, inventory, slotIndex, value, p);
            },
            () => CloseEditor(componentCharacter)
        );
        componentCharacter.Entity.FindComponent<ComponentGui>(true)!.ModalPanelWidget = editor;
    }

    private void EditTruthTableCircuitBlock(int x, int y, int z, int value, ComponentCharacter componentCharacter)
    {
        var source = _truthTable.GetBlockData(new Point3(x, y, z));
        var truthTableData = CreateTruthTableWorkingCopy(source);
        var editor = new TruthTableEditorWidget(
            truthTableData,
            saved =>
            {
                if (!saved)
                {
                    return;
                }

                var face =
                    ((TruthTableCircuitBlock)WorldContentBinding.For(Project).Catalog.Blocks[188])
                    .GetFace(value);
                var cell = new CellFace(x, y, z, face);
                var p = EditableBlockPackageBuilder.Capture(cell, false, 0, 0, truthTableData);
                SubmitBlock(componentCharacter, new Point3(x, y, z), value, p);
            },
            () => CloseEditor(componentCharacter)
        );
        componentCharacter.Entity.FindComponent<ComponentGui>(true)!.ModalPanelWidget = editor;
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_battery is not null)
        {
            _battery.InventoryEditRequested -= OnInventoryEdit;
            _battery.BlockEditRequested -= OnBlockEdit;
        }

        if (_piston is not null)
        {
            _piston.InventoryEditRequested -= OnInventoryEdit;
            _piston.BlockEditRequested -= OnBlockEdit;
        }

        if (_delay is not null)
        {
            _delay.InventoryEditRequested -= OnInventoryEdit;
            _delay.BlockEditRequested -= OnBlockEdit;
        }

        if (_memory is not null)
        {
            _memory.InventoryEditRequested -= OnInventoryEdit;
            _memory.BlockEditRequested -= OnBlockEdit;
        }

        if (_truthTable is not null)
        {
            _truthTable.InventoryEditRequested -= OnInventoryEdit;
            _truthTable.BlockEditRequested -= OnBlockEdit;
        }

        base.Dispose();
    }
}
