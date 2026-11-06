using Game.Subsystems.Circuits;

namespace Game.Subsystems.Behavior;

public class SubsystemBatteryBlockBehavior : SubsystemBlockBehavior
{
    public event Action<CircuitInventoryEditRequest>? InventoryEditRequested;

    public event Action<CircuitBlockEditRequest>? BlockEditRequested;

    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:battery")];

    public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentCharacter componentCharacter)
    {
        if (componentCharacter.IsLocallyControlled)
        {
            InventoryEditRequested?.Invoke(
                new CircuitInventoryEditRequest(
                    componentCharacter,
                    inventory,
                    slotIndex,
                    inventory.GetSlotValue(slotIndex),
                    BuiltInBlockEditKind.Battery
                )
            );
        }

        return true;
    }

    public override bool OnEditBlock(int x, int y, int z, int value, ComponentCharacter componentCharacter)
    {
        if (componentCharacter.IsLocallyControlled)
        {
            BlockEditRequested?.Invoke(
                new CircuitBlockEditRequest(
                    componentCharacter,
                    new Point3(x, y, z),
                    value,
                    BuiltInBlockEditKind.Battery
                )
            );
        }

        return true;
    }

    public override void Dispose()
    {
        InventoryEditRequested = null;
        BlockEditRequested = null;
        base.Dispose();
    }
}
