namespace Game.Subsystems.Circuits;

public readonly record struct CircuitInventoryEditRequest(
    ComponentCharacter Character,
    IInventory Inventory,
    int SlotIndex,
    int OriginalValue,
    BuiltInBlockEditKind Kind
);
