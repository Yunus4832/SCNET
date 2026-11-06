using Game.Subsystems.Circuits;

namespace Game.Subsystems.Behavior;

public class SubsystemTruthTableCircuitBlockBehavior() : SubsystemEditableItemBehavior<TruthTableData>(188)
{
    public event Action<CircuitInventoryEditRequest>? InventoryEditRequested;

    public event Action<CircuitBlockEditRequest>? BlockEditRequested;

    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:truth_table_circuit")];

    protected override EditableBlockPackage CreateBlockStatePackage(Point3 point, TruthTableData data)
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
                    BuiltInBlockEditKind.TruthTable
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
                    BuiltInBlockEditKind.TruthTable
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
