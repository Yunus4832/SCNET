using Game.Subsystems.Circuits;

namespace Game.Subsystems.Behavior;

public class SubsystemMemoryBankBlockBehavior() : SubsystemEditableItemBehavior<MemoryBankData>(186)
{
    public event Action<CircuitInventoryEditRequest>? InventoryEditRequested;

    public event Action<CircuitBlockEditRequest>? BlockEditRequested;

    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:memory_bank")];

    protected override EditableBlockPackage CreateBlockStatePackage(Point3 point, MemoryBankData data)
    {
        return EditableBlockPackageBuilder.Capture(new CellFace(point.X, point.Y, point.Z, 0), false, 0, 0, data);
    }

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
                    BuiltInBlockEditKind.MemoryBank
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
                    BuiltInBlockEditKind.MemoryBank
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
