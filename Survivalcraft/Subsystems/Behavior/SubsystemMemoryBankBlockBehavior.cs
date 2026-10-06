using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;
using Game.Network.Packages.Handlers;

namespace Game.Subsystems;

public class SubsystemMemoryBankBlockBehavior() : SubsystemEditableItemBehavior<MemoryBankData>(186)
{
    public override int[] HandledBlocks => [186];

    protected override EditableBlockPackage CreateBlockStatePackage(Point3 point, MemoryBankData data)
    {
        return new EditableBlockPackage(new CellFace(point.X, point.Y, point.Z, 0), false, 0, 0, data);
    }

    public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
    {
        var value = inventory.GetSlotValue(slotIndex);
        inventory.GetSlotCount(slotIndex);
        var id = Terrain.ExtractData(value);
        var memoryBankData = GetItemData(id);
        memoryBankData = memoryBankData != null ? (MemoryBankData)memoryBankData.Copy() : new MemoryBankData();
        var editor = new MemoryBankEditorWidget(memoryBankData, saved =>
        {
            if (!saved)
            {
                return;
            }

            var p = new EditableBlockPackage(default, true, inventory.Id, slotIndex, memoryBankData);
            EditableBlockPackageHandler.Submit(p);
        }, () => componentPlayer.ComponentGui.ModalPanelWidget = null);
        componentPlayer.ComponentGui.ModalPanelWidget = editor;
        return true;
    }

    public override bool OnEditBlock(int x, int y, int z, int value, ComponentPlayer componentPlayer)
    {
        var memoryBankData = GetBlockData(new Point3(x, y, z)) ?? new MemoryBankData();
        var editor = new MemoryBankEditorWidget(memoryBankData, saved =>
        {
            if (!saved)
            {
                return;
            }

            var face = ((MemoryBankBlock)BlocksManager.Blocks[186]).GetFace(value);
            var cell = new CellFace(x, y, z, face);
            var p = new EditableBlockPackage(cell, false, 0, 0, memoryBankData);
            EditableBlockPackageHandler.Submit(p);
        }, () => componentPlayer.ComponentGui.ModalPanelWidget = null);
        componentPlayer.ComponentGui.ModalPanelWidget = editor;
        return true;
    }
}
