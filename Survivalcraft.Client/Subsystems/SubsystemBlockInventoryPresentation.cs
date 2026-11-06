using EntitySystem.Core;

namespace Game.Subsystems;

/// <summary>WorldConnection world consumer of the authority-approved inventory-open reply.</summary>
public sealed class SubsystemBlockInventoryPresentation : Subsystem
{
    private bool _disposed;

    public override void Dispose() => _disposed = true;

    public bool OpenInventory(int inventoryId)
    {
        if (_disposed ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation)
        {
            return false;
        }

        var player = Project.FindSubsystem<SubsystemCharacters>(true)!.LocalCharacter;
        if (player is null) { return false; }

        var inventory = Project.FindSubsystem<SubsystemInventories>(true)!.GetInventoryById(inventoryId);
        Widget? panel = inventory switch
        {
            ComponentChest chest => new ChestWidget(player.ComponentMiner.Inventory, chest),
            ComponentFurnace furnace => new FurnaceWidget(player.ComponentMiner.Inventory, furnace),
            ComponentDispenser dispenser => new DispenserWidget(player.ComponentMiner.Inventory, dispenser),
            ComponentCraftingTable crafting => new CraftingTableWidget(player.ComponentMiner.Inventory, crafting),
            _ => null
        };
        if (panel is null) { return false; }

        player.Entity.FindComponent<ComponentGui>(true)!.ModalPanelWidget = panel;
        AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
        return true;
    }
}
