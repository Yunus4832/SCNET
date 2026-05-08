using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Subsystems;

public class SubsystemButtonBlockBehavior : SubsystemBlockBehavior
{
    public override int[] HandledBlocks => [142];

    public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
    {
        var value = inventory.GetSlotValue(slotIndex);
        inventory.GetSlotCount(slotIndex);
        var data = Terrain.ExtractData(value);
        var voltageLevel = ButtonBlock.GetVoltageLevel(data);
        DialogsManager.ShowDialog(componentPlayer.GuiWidget, new EditVoltageLevelDialog(voltageLevel,
            delegate(int newVoltageLevel)
            {
                var data2 = ButtonBlock.SetVoltageLevel(data, newVoltageLevel);
                var num = Terrain.ReplaceData(value, data2);
                if (num == value)
                {
                    return;
                }

                var p = new EditableBlockPackage(EditableItemType.Button, default, true, inventory.Id, slotIndex,
                    newVoltageLevel);
                CommonLib.Net.QueuePackage(p);
                if (CommonLib.WorkType != WorkType.Client)
                {
                    p.Handle(ProjectNet.Project, CommonLib.Net, false);
                }
            }));
        return true;
    }

    public override bool OnEditBlock(int x, int y, int z, int value, ComponentPlayer componentPlayer)
    {
        var data = Terrain.ExtractData(value);
        var voltageLevel = ButtonBlock.GetVoltageLevel(data);
        DialogsManager.ShowDialog(
            componentPlayer.GuiWidget,
            new EditVoltageLevelDialog(voltageLevel,
                delegate(int newVoltageLevel)
                {
                    var num = ButtonBlock.SetVoltageLevel(data, newVoltageLevel);
                    if (num == data)
                    {
                        return;
                    }

                    const int face = 4;
                    var cell = new CellFace(x, y, z, face);
                    var p = new EditableBlockPackage(
                        EditableItemType.Button,
                        cell,
                        false,
                        0,
                        0,
                        newVoltageLevel
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
