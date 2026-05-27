using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public class SubsystemBatteryBlockBehavior : SubsystemBlockBehavior
{
    public override int[] HandledBlocks => [138];

    public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
    {
        var value = inventory.GetSlotValue(slotIndex);
        inventory.GetSlotCount(slotIndex);
        var data = Terrain.ExtractData(value);
        var voltageLevel = BatteryBlock.GetVoltageLevel(data);
        DialogsManager.ShowDialog(componentPlayer.GuiWidget, new EditBatteryDialog(voltageLevel,
            delegate(int newVoltageLevel)
            {
                var data2 = BatteryBlock.SetVoltageLevel(data, newVoltageLevel);
                var num = Terrain.ReplaceData(value, data2);
                if (num == value)
                {
                    return;
                }

                var p = new EditableBlockPackage(EditableItemType.Battery, default, true, inventory.Id, slotIndex,
                    newVoltageLevel);
                CommonLib.Net.QueuePackage(p);
                if (CommonLib.WorkType != WorkType.Client)
                {
                    p.Handle(CommonLib.Net, false);
                }
            }));
        return true;
    }

    public override bool OnEditBlock(int x, int y, int z, int value, ComponentPlayer componentPlayer)
    {
        var data = Terrain.ExtractData(value);
        var voltageLevel = BatteryBlock.GetVoltageLevel(data);
        DialogsManager.ShowDialog(componentPlayer.GuiWidget, new EditBatteryDialog(voltageLevel,
            delegate(int newVoltageLevel)
            {
                var num = BatteryBlock.SetVoltageLevel(data, newVoltageLevel);
                if (num == data)
                {
                    return;
                }

                const int face = 4;
                var cell = new CellFace(x, y, z, face);
                var p = new EditableBlockPackage(
                    EditableItemType.Battery,
                    cell,
                    false,
                    0,
                    0,
                    newVoltageLevel
                );
                CommonLib.Net.QueuePackage(p);
                if (CommonLib.WorkType != WorkType.Client)
                {
                    p.Handle(CommonLib.Net, false);
                }
            }));
        return true;
    }
}
