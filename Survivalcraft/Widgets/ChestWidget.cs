using System.Xml.Linq;

namespace Game.Widgets;

public class ChestWidget : CanvasWidget
{
    private readonly GridPanelWidget _chestGrid;

    private readonly ComponentChest _componentChest;

    private readonly GridPanelWidget _inventoryGrid;

    public ChestWidget(IInventory inventory, ComponentChest componentChest)
    {
        _componentChest = componentChest;
        var node = ContentManager.Get<XElement>("Widgets/ChestWidget");
        LoadContents(this, node);
        _inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid")!;
        _chestGrid = Children.Find<GridPanelWidget>("ChestGrid")!;
        var num = 0;
        for (var i = 0; i < _chestGrid.RowsCount; i++)
        for (var j = 0; j < _chestGrid.ColumnsCount; j++)
        {
            var inventorySlotWidget = new InventorySlotWidget();
            inventorySlotWidget.AssignInventorySlot(componentChest, num++);
            _chestGrid.Children.Add(inventorySlotWidget);
            _chestGrid.SetWidgetCell(inventorySlotWidget, new Point2(j, i));
        }

        num = 10;
        for (var k = 0; k < _inventoryGrid.RowsCount; k++)
        for (var l = 0; l < _inventoryGrid.ColumnsCount; l++)
        {
            var inventorySlotWidget2 = new InventorySlotWidget();
            inventorySlotWidget2.AssignInventorySlot(inventory, num++);
            _inventoryGrid.Children.Add(inventorySlotWidget2);
            _inventoryGrid.SetWidgetCell(inventorySlotWidget2, new Point2(l, k));
        }
    }

    public override void Update()
    {
        if (!_componentChest.IsAddedToProject)
        {
            ParentWidget?.Children.Remove(this);
        }
    }
}
