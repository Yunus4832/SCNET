using Game.Network.Handling;

namespace Game.Subsystems;

public class SubsystemButtonBlockBehavior : SubsystemBlockBehavior
{
    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:button")];

    public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
    {
        var value = inventory.GetSlotValue(slotIndex);
        inventory.GetSlotCount(slotIndex);
        var data = Terrain.ExtractData(value);
        var voltageLevel = ButtonBlock.GetVoltageLevel(data);
        DialogsManager.ShowDialog(componentPlayer.GuiWidget, new EditVoltageLevelDialog(voltageLevel,
            delegate (int newVoltageLevel)
            {
                var data2 = ButtonBlock.SetVoltageLevel(data, newVoltageLevel);
                var num = Terrain.ReplaceData(value, data2);
                if (num == value)
                {
                    return;
                }

                var p = new EditableBlockPackage(BuiltInBlockEditKind.Button, default, true, inventory.Id, slotIndex,
                    newVoltageLevel);
                EditableBlockPackageHandler.Submit(p);
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
                delegate (int newVoltageLevel)
                {
                    var num = ButtonBlock.SetVoltageLevel(data, newVoltageLevel);
                    if (num == data)
                    {
                        return;
                    }

                    const int face = 4;
                    var cell = new CellFace(x, y, z, face);
                    var p = new EditableBlockPackage(
                        BuiltInBlockEditKind.Button,
                        cell,
                        false,
                        0,
                        0,
                        newVoltageLevel
                    );
                    EditableBlockPackageHandler.Submit(p);
                }));
        return true;
    }
}
