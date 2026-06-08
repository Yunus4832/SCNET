using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public class SubsystemTruthTableCircuitBlockBehavior() : SubsystemEditableItemBehavior<TruthTableData>(188)
{
    public override int[] HandledBlocks => [188];

    public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
    {
        var value = inventory.GetSlotValue(slotIndex);
        inventory.GetSlotCount(slotIndex);
        var id = Terrain.ExtractData(value);
        var truthTableData = GetItemData(id);
        truthTableData = truthTableData != null ? (TruthTableData)truthTableData.Copy() : new TruthTableData();
        DialogsManager.ShowDialog(componentPlayer.GuiWidget, new EditTruthTableDialog(truthTableData, delegate
        {
            var p = new EditableBlockPackage(default, true, inventory.Id, slotIndex, truthTableData);
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
        var truthTableData = GetBlockData(new Point3(x, y, z)) ?? new TruthTableData();
        DialogsManager.ShowDialog(componentPlayer.GuiWidget, new EditTruthTableDialog(truthTableData, delegate
        {
            var face = ((TruthTableCircuitBlock)BlocksManager.Blocks[188]).GetFace(value);
            var cell = new CellFace(x, y, z, face);
            var p = new EditableBlockPackage(cell, false, 0, 0, truthTableData);
            CommonLib.Net.QueuePackage(p);
            if (CommonLib.WorkType != WorkType.Client)
            {
                PackageDispatcher.Handle(p, CommonLib.Net, false);
            }
        }));
        return true;
    }
}
