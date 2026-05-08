using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Subsystems;

public class SubsystemAdjustableDelayGateBlockBehavior : SubsystemBlockBehavior
{
    public override int[] HandledBlocks => [224];

    public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
    {
        var value = inventory.GetSlotValue(slotIndex);
        inventory.GetSlotCount(slotIndex);
        var data = Terrain.ExtractData(value);
        var delay = AdjustableDelayGateBlock.GetDelay(data);
        DialogsManager.ShowDialog(componentPlayer.GuiWidget, new EditAdjustableDelayGateDialog(delay,
            delegate(int newDelay)
            {
                var data2 = AdjustableDelayGateBlock.SetDelay(data, newDelay);
                var num = Terrain.ReplaceData(value, data2);
                if (num != value)
                {
                    var p = new EditableBlockPackage(EditableItemType.AdjustableDelayGate, default, true, inventory.Id,
                        slotIndex, newDelay);
                    CommonLib.Net.QueuePackage(p);
                    if (CommonLib.WorkType != WorkType.Client)
                    {
                        p.Handle(ProjectNet.Project, CommonLib.Net, false);
                    }
                }
            }));
        return true;
    }

    public override bool OnEditBlock(int x, int y, int z, int value, ComponentPlayer componentPlayer)
    {
        var data = Terrain.ExtractData(value);
        var delay = AdjustableDelayGateBlock.GetDelay(data);
        DialogsManager.ShowDialog(
            componentPlayer.GuiWidget,
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
                        ((AdjustableDelayGateBlock)BlocksManager.Blocks[AdjustableDelayGateBlock.Index]).GetFace(value);
                    var cell = new CellFace(x, y, z, face);
                    var p = new EditableBlockPackage(
                        EditableItemType.AdjustableDelayGate,
                        cell,
                        false,
                        0,
                        0,
                        newDelay
                    );
                    CommonLib.Net.QueuePackage(p);
                    if (CommonLib.WorkType != WorkType.Client)
                    {
                        p.Handle(ProjectNet.Project, CommonLib.Net, false);
                    }
                }));
        return true;
    }
}
