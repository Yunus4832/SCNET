using System.Xml.Linq;

namespace Game.Widgets;

public class MusketWidget : CanvasWidget
{
    private const string _typeName = "MusketWidget";

    private readonly LabelWidget _instructionsLabel;

    private readonly IInventory _inventory;

    private readonly GridPanelWidget _inventoryGrid;

    private readonly InventorySlotWidget _inventorySlotWidget;

    private readonly int _slotIndex;

    public MusketWidget(IInventory inventory, int slotIndex)
    {
        _inventory = inventory;
        _slotIndex = slotIndex;
        var node = ContentManager.Get<XElement>("Widgets/MusketWidget");
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
            Matrix.CreateLookAt(new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 0f), -Vector3.UnitZ);
    }

    public override void Update()
    {
        var slotValue = _inventory.GetSlotValue(_slotIndex);
        var slotCount = _inventory.GetSlotCount(_slotIndex);
        if (Terrain.ExtractContents(slotValue) == 212 && slotCount > 0)
        {
            _instructionsLabel.Text = MusketBlock.GetLoadState(Terrain.ExtractData(slotValue)) switch
            {
                MusketBlock.LoadState.Empty => LanguageManager.Get(_typeName, 0),
                MusketBlock.LoadState.Gunpowder => LanguageManager.Get(_typeName, 1),
                MusketBlock.LoadState.Wad => LanguageManager.Get(_typeName, 2),
                MusketBlock.LoadState.Loaded => LanguageManager.Get(_typeName, 3),
                _ => string.Empty
            };
        }
        else
        {
            ParentWidget?.Children.Remove(this);
        }
    }
}
