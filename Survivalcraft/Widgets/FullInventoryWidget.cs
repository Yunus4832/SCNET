using System.Xml.Linq;

namespace Game.Widgets;

public class FullInventoryWidget : CanvasWidget
{
    private readonly GridPanelWidget _craftingGrid;

    private readonly InventorySlotWidget _craftingRemainsSlot;

    private readonly InventorySlotWidget _craftingResultSlot;

    private readonly GridPanelWidget _inventoryGrid;

    public FullInventoryWidget(IInventory? inventory, ComponentCraftingTable componentCraftingTable)
    {
        var node = ContentManager.Get<XElement>("Widgets/FullInventoryWidget");
        LoadContents(this, node);
        _inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid")!;
        _craftingGrid = Children.Find<GridPanelWidget>("CraftingGrid")!;
        _craftingResultSlot = Children.Find<InventorySlotWidget>("CraftingResultSlot")!;
        _craftingRemainsSlot = Children.Find<InventorySlotWidget>("CraftingRemainsSlot")!;
        var num = 10;
        for (var i = 0; i < _inventoryGrid.RowsCount; i++)
        for (var j = 0; j < _inventoryGrid.ColumnsCount; j++)
        {
            var inventorySlotWidget = new InventorySlotWidget();
            inventorySlotWidget.AssignInventorySlot(inventory, num++);
            _inventoryGrid.Children.Add(inventorySlotWidget);
            _inventoryGrid.SetWidgetCell(inventorySlotWidget, new Point2(j, i));
        }

        num = 0;
        for (var k = 0; k < _craftingGrid.RowsCount; k++)
        for (var l = 0; l < _craftingGrid.ColumnsCount; l++)
        {
            var inventorySlotWidget2 = new InventorySlotWidget();
            inventorySlotWidget2.AssignInventorySlot(componentCraftingTable, num++);
            _craftingGrid.Children.Add(inventorySlotWidget2);
            _craftingGrid.SetWidgetCell(inventorySlotWidget2, new Point2(l, k));
        }

        _craftingResultSlot.AssignInventorySlot(componentCraftingTable, componentCraftingTable.ResultSlotIndex);
        _craftingRemainsSlot.AssignInventorySlot(componentCraftingTable, componentCraftingTable.RemainsSlotIndex);
    }
}
