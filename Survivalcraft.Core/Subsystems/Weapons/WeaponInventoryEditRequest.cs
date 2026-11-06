namespace Game.Subsystems;

public readonly record struct WeaponInventoryEditRequest(
    ComponentCharacter Character,
    IInventory Inventory,
    int SlotIndex,
    WeaponInventoryKind Kind
);
