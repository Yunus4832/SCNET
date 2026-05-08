using System.Xml.Linq;

namespace Game.Widgets;

public class BowWidget : CanvasWidget
{
    private const string _typeName = "BowWidget";

    private readonly LabelWidget _instructionsLabel;

    private readonly IInventory _inventory;

    private readonly GridPanelWidget _inventoryGrid;

    private readonly InventorySlotWidget _inventorySlotWidget;

    private readonly int _slotIndex;

    public BowWidget(IInventory inventory, int slotIndex)
    {
        _inventory = inventory;
        _slotIndex = slotIndex;
        var node = ContentManager.Get<XElement>("Widgets/BowWidget");
        LoadContents(this, node);
        _inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid")!;
        _inventorySlotWidget = Children.Find<InventorySlotWidget>("InventorySlot")!;
        _instructionsLabel = Children.Find<LabelWidget>("InstructionsLabel")!;
        for (var i = 0; i < _inventoryGrid.RowsCount; i++)
        for (var j = 0; j < _inventoryGrid.ColumnsCount; j++)
        {
            var widget = new InventorySlotWidget();
            _inventoryGrid.Children.Add(widget);
            _inventoryGrid.SetWidgetCell(widget, new Point2(j, i));
        }

        var num = 10;
        foreach (var child in _inventoryGrid.Children)
        {
            (child as InventorySlotWidget)?.AssignInventorySlot(inventory, num++);
        }

        _inventorySlotWidget.AssignInventorySlot(inventory, slotIndex);
        _inventorySlotWidget.CustomViewMatrix =
            Matrix.CreateLookAt(new Vector3(-1f, 0.2f, 0.6f), new Vector3(0f, 0.2f, 0f), Vector3.UnitY);
    }

    public override void Update()
    {
        var slotValue = _inventory.GetSlotValue(_slotIndex);
        var slotCount = _inventory.GetSlotCount(_slotIndex);
        var num = Terrain.ExtractContents(slotValue);
        _instructionsLabel.Text = !BowBlock.GetArrowType(Terrain.ExtractData(slotValue)).HasValue
            ? LanguageControl.Get(_typeName, 0)
            : LanguageControl.Get(_typeName, 1);
        if (num != 191 || slotCount == 0)
        {
            ParentWidget?.Children.Remove(this);
        }
    }
}
