using System.Xml.Linq;

namespace Game.Widgets;

public class ShortInventoryWidget : CanvasWidget
{
    private IInventory? _inventory;

    private readonly GridPanelWidget _inventoryGrid;

    public ShortInventoryWidget()
    {
        var node = ContentManager.Get<XElement>("Widgets/ShortInventoryWidget");
        LoadContents(this, node);
        _inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid")!;
    }

    public void AssignComponents(IInventory? inventory)
    {
        if (inventory == _inventory)
        {
            return;
        }

        _inventory = inventory;
        _inventoryGrid.Children.Clear();
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        if (_inventory == null)
        {
            return;
        }

        var max = _inventory is ComponentCreativeInventory ? 10 : 7;
        _inventory.VisibleSlotsCount = MathUtils.Clamp((int)((parentAvailableSize.X - 320f - 25f) / 72f), 7, max);
        if (_inventory.VisibleSlotsCount != _inventoryGrid.Children.Count)
        {
            _inventoryGrid.Children.Clear();
            _inventoryGrid.RowsCount = 1;
            _inventoryGrid.ColumnsCount = _inventory.VisibleSlotsCount;
            for (var i = 0; i < _inventoryGrid.ColumnsCount; i++)
            {
                var inventorySlotWidget = new InventorySlotWidget();
                inventorySlotWidget.AssignInventorySlot(_inventory, i);
                inventorySlotWidget.BevelColor = new Color(181, 172, 154) * 0.6f;
                inventorySlotWidget.CenterColor = new Color(181, 172, 154) * 0.33f;
                _inventoryGrid.Children.Add(inventorySlotWidget);
                _inventoryGrid.SetWidgetCell(inventorySlotWidget, new Point2(i, 0));
            }
        }

        base.MeasureOverride(parentAvailableSize);
    }
}
