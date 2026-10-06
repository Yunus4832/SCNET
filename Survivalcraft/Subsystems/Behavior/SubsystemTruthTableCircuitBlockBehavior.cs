using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;
using Game.Network.Packages.Handlers;

namespace Game.Subsystems;

public class SubsystemTruthTableCircuitBlockBehavior() : SubsystemEditableItemBehavior<TruthTableData>(188)
{
    public override int[] HandledBlocks => [188];

    protected override EditableBlockPackage CreateBlockStatePackage(Point3 point, TruthTableData data)
    {
        return new EditableBlockPackage(new CellFace(point.X, point.Y, point.Z, 0), false, 0, 0, data);
    }

    public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
    {
        var value = inventory.GetSlotValue(slotIndex);
        inventory.GetSlotCount(slotIndex);
        var id = Terrain.ExtractData(value);
        var truthTableData = GetItemData(id);
        truthTableData = truthTableData != null ? (TruthTableData)truthTableData.Copy() : new TruthTableData();
        var editor = new TruthTableEditorWidget(truthTableData, saved =>
        {
            if (!saved)
            {
                return;
            }

            var p = new EditableBlockPackage(default, true, inventory.Id, slotIndex, truthTableData);
            EditableBlockPackageHandler.Submit(p);
        }, () => componentPlayer.ComponentGui.ModalPanelWidget = null);
        componentPlayer.ComponentGui.ModalPanelWidget = editor;
        return true;
    }

    public override bool OnEditBlock(int x, int y, int z, int value, ComponentPlayer componentPlayer)
    {
        var truthTableData = GetBlockData(new Point3(x, y, z)) ?? new TruthTableData();
        var editor = new TruthTableEditorWidget(truthTableData, saved =>
        {
            if (!saved)
            {
                return;
            }

            var face = ((TruthTableCircuitBlock)BlocksManager.Blocks[188]).GetFace(value);
            var cell = new CellFace(x, y, z, face);
            var p = new EditableBlockPackage(cell, false, 0, 0, truthTableData);
            EditableBlockPackageHandler.Submit(p);
        }, () => componentPlayer.ComponentGui.ModalPanelWidget = null);
        componentPlayer.ComponentGui.ModalPanelWidget = editor;
        return true;
    }
}
