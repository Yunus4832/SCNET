using Game.Network.Handling;

namespace Game.Subsystems;

public class SubsystemBatteryBlockBehavior : SubsystemBlockBehavior
{
    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:battery")];

    public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentCharacter componentCharacter)
    {
        var value = inventory.GetSlotValue(slotIndex);
        inventory.GetSlotCount(slotIndex);
        var data = Terrain.ExtractData(value);
        var voltageLevel = BatteryBlock.GetVoltageLevel(data);
        DialogsManager.ShowDialog(
            componentCharacter.GuiWidget,
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

                    var p = new EditableBlockPackage(
                        BuiltInBlockEditKind.Battery,
                        default,
                        true,
                        inventory.Id,
                        slotIndex,
                        newVoltageLevel
                    );
                    EditableBlockPackageHandler.Submit(p);
                }
            )
        );
        return true;
    }

    public override bool OnEditBlock(int x, int y, int z, int value, ComponentCharacter componentCharacter)
    {
        var data = Terrain.ExtractData(value);
        var voltageLevel = BatteryBlock.GetVoltageLevel(data);
        DialogsManager.ShowDialog(
            componentCharacter.GuiWidget,
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
                    var p = new EditableBlockPackage(
                        BuiltInBlockEditKind.Battery,
                        cell,
                        false,
                        0,
                        0,
                        newVoltageLevel
                    );
                    EditableBlockPackageHandler.Submit(p);
                }
            )
        );
        return true;
    }
}
