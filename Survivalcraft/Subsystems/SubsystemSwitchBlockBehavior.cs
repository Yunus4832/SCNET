using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Subsystems;

public class SubsystemSwitchBlockBehavior : SubsystemBlockBehavior
{
    public override int[] HandledBlocks => [141];

    public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
    {
        var value = inventory.GetSlotValue(slotIndex);
        inventory.GetSlotCount(slotIndex);
        var data = Terrain.ExtractData(value);
        var voltageLevel = SwitchBlock.GetVoltageLevel(data);
        DialogsManager.ShowDialog(componentPlayer.GuiWidget, new EditVoltageLevelDialog(voltageLevel,
            delegate(int newVoltageLevel)
            {
                var data2 = SwitchBlock.SetVoltageLevel(data, newVoltageLevel);
                var num = Terrain.ReplaceData(value, data2);
                if (num != value)
                {
                    var p = new EditableBlockPackage(EditableItemType.Switch, default, true, inventory.Id, slotIndex,
                        newVoltageLevel);
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
        var voltageLevel = SwitchBlock.GetVoltageLevel(data);
        DialogsManager.ShowDialog(
            componentPlayer.GuiWidget,
            new EditVoltageLevelDialog(voltageLevel,
                delegate(int newVoltageLevel)
                {
                    var num = SwitchBlock.SetVoltageLevel(data, newVoltageLevel);
                    if (num == data)
                    {
                        return;
                    }

                    const int face = 4;
                    var cell = new CellFace(x, y, z, face);
                    var p = new EditableBlockPackage(EditableItemType.Switch, cell, false, 0, 0,
                        newVoltageLevel);
                    CommonLib.Net.QueuePackage(p);
                    if (CommonLib.WorkType != WorkType.Client)
                    {
                        p.Handle(ProjectNet.Project, CommonLib.Net, false);
                    }
                }
            )
        );
        return true;
    }
}
