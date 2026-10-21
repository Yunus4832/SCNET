using Game.Network.Handling;

namespace Game.Subsystems;

public class SubsystemSwitchBlockBehavior : SubsystemBlockBehavior
{
    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:switch")];

    public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
    {
        var value = inventory.GetSlotValue(slotIndex);
        inventory.GetSlotCount(slotIndex);
        var data = Terrain.ExtractData(value);
        var voltageLevel = SwitchBlock.GetVoltageLevel(data);
        DialogsManager.ShowDialog(
            componentPlayer.GuiWidget,
            new EditVoltageLevelDialog(
                voltageLevel,
                delegate(int newVoltageLevel)
                {
                    var data2 = SwitchBlock.SetVoltageLevel(data, newVoltageLevel);
                    var num = Terrain.ReplaceData(value, data2);
                    if (num != value)
                    {
                        var p = new EditableBlockPackage(
                            BuiltInBlockEditKind.Switch,
                            default,
                            true,
                            inventory.Id,
                            slotIndex,
                            newVoltageLevel
                        );
                        EditableBlockPackageHandler.Submit(p);
                    }
                }
            )
        );
        return true;
    }

    public override bool OnEditBlock(int x, int y, int z, int value, ComponentPlayer componentPlayer)
    {
        var data = Terrain.ExtractData(value);
        var voltageLevel = SwitchBlock.GetVoltageLevel(data);
        DialogsManager.ShowDialog(
            componentPlayer.GuiWidget,
            new EditVoltageLevelDialog(
                voltageLevel,
                delegate(int newVoltageLevel)
                {
                    var num = SwitchBlock.SetVoltageLevel(data, newVoltageLevel);
                    if (num == data)
                    {
                        return;
                    }

                    const int face = 4;
                    var cell = new CellFace(x, y, z, face);
                    var p = new EditableBlockPackage(
                        BuiltInBlockEditKind.Switch,
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
