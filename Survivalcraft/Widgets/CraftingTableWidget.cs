using System.Xml.Linq;

namespace Game.Widgets;

public class CraftingTableWidget : CanvasWidget
{
    private readonly ComponentCraftingTable _componentCraftingTable;

    private readonly GridPanelWidget _craftingGrid;

    private readonly InventorySlotWidget _craftingRemainsSlot;

    private readonly InventorySlotWidget _craftingResultSlot;

    private readonly GridPanelWidget _inventoryGrid;

    public CraftingTableWidget(IInventory inventory, ComponentCraftingTable componentCraftingTable)
    {
        _componentCraftingTable = componentCraftingTable;
        var node = ContentManager.Get<XElement>("Widgets/CraftingTableWidget");
        LoadContents(this, node);
        _inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid")!;
        _craftingGrid = Children.Find<GridPanelWidget>("CraftingGrid")!;
        _craftingResultSlot = Children.Find<InventorySlotWidget>("CraftingResultSlot")!;
        _craftingRemainsSlot = Children.Find<InventorySlotWidget>("CraftingRemainsSlot")!;
        var num = 10;
        for (var i = 0; i < _inventoryGrid.RowsCount; i++)
        {
            for (var j = 0; j < _inventoryGrid.ColumnsCount; j++)
            {
                var inventorySlotWidget = new InventorySlotWidget();
                inventorySlotWidget.AssignInventorySlot(inventory, num++);
                _inventoryGrid.Children.Add(inventorySlotWidget);
                _inventoryGrid.SetWidgetCell(inventorySlotWidget, new Point2(j, i));
            }
        }

        num = 0;
        for (var k = 0; k < _craftingGrid.RowsCount; k++)
        {
            for (var l = 0; l < _craftingGrid.ColumnsCount; l++)
            {
                var inventorySlotWidget2 = new InventorySlotWidget();
                inventorySlotWidget2.AssignInventorySlot(_componentCraftingTable, num++);
                _craftingGrid.Children.Add(inventorySlotWidget2);
                _craftingGrid.SetWidgetCell(inventorySlotWidget2, new Point2(l, k));
            }
        }

        _craftingResultSlot.AssignInventorySlot(_componentCraftingTable, _componentCraftingTable.ResultSlotIndex);
        _craftingRemainsSlot.AssignInventorySlot(_componentCraftingTable, _componentCraftingTable.RemainsSlotIndex);
    }

    public override void Update()
    {
        if (!_componentCraftingTable.IsAddedToProject)
        {
            ParentWidget?.Children.Remove(this);
        }
    }
}
