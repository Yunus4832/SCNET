using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public class SubsystemMemoryBankBlockBehavior() : SubsystemEditableItemBehavior<MemoryBankData>(186)
{
    public override int[] HandledBlocks => [186];

    public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
    {
        var value = inventory.GetSlotValue(slotIndex);
        inventory.GetSlotCount(slotIndex);
        var id = Terrain.ExtractData(value);
        var memoryBankData = GetItemData(id);
        memoryBankData = memoryBankData != null ? (MemoryBankData)memoryBankData.Copy() : new MemoryBankData();
        DialogsManager.ShowDialog(componentPlayer.GuiWidget, new EditMemoryBankDialogApi(memoryBankData, delegate
        {
            var p = new EditableBlockPackage(default, true, inventory.Id, slotIndex, memoryBankData);
            CommonLib.Net.QueuePackage(p);
            if (CommonLib.WorkType != WorkType.Client)
            {
                PackageDispatcher.Handle(p, CommonLib.Net, false);
            }
        }));
        return true;
    }

    public override bool OnEditBlock(int x, int y, int z, int value, ComponentPlayer componentPlayer)
    {
        var memoryBankData = GetBlockData(new Point3(x, y, z)) ?? new MemoryBankData();
        DialogsManager.ShowDialog(componentPlayer.GuiWidget, new EditMemoryBankDialogApi(memoryBankData, delegate
        {
            var face = ((MemoryBankBlock)BlocksManager.Blocks[186]).GetFace(value);
            var cell = new CellFace(x, y, z, face);
            var p = new EditableBlockPackage(cell, false, 0, 0, memoryBankData);
            CommonLib.Net.QueuePackage(p);
            if (CommonLib.WorkType != WorkType.Client)
            {
                PackageDispatcher.Handle(p, CommonLib.Net, false);
            }
        }));
        return true;
    }
}
