using System.Xml.Linq;

namespace Game.Widgets;

public class FurnaceWidget : CanvasWidget
{
    private readonly ComponentFurnace _componentFurnace;

    private readonly FireWidget _fire;

    private readonly InventorySlotWidget _fuelSlot;

    private readonly GridPanelWidget _furnaceGrid;

    private readonly GridPanelWidget _inventoryGrid;

    private readonly ValueBarWidget _progress;

    private readonly InventorySlotWidget _remainsSlot;

    private readonly InventorySlotWidget _resultSlot;

    public FurnaceWidget(IInventory inventory, ComponentFurnace componentFurnace)
    {
        _componentFurnace = componentFurnace;
        var node = ContentManager.Get<XElement>("Widgets/FurnaceWidget");
        LoadContents(this, node);
        _inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid")!;
        _furnaceGrid = Children.Find<GridPanelWidget>("FurnaceGrid")!;
        _fire = Children.Find<FireWidget>("Fire")!;
        _progress = Children.Find<ValueBarWidget>("Progress")!;
        _resultSlot = Children.Find<InventorySlotWidget>("ResultSlot")!;
        _remainsSlot = Children.Find<InventorySlotWidget>("RemainsSlot")!;
        _fuelSlot = Children.Find<InventorySlotWidget>("FuelSlot")!;
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
        for (var k = 0; k < _furnaceGrid.RowsCount; k++)
        for (var l = 0; l < _furnaceGrid.ColumnsCount; l++)
        {
            var inventorySlotWidget2 = new InventorySlotWidget();
            inventorySlotWidget2.AssignInventorySlot(componentFurnace, num++);
            _furnaceGrid.Children.Add(inventorySlotWidget2);
            _furnaceGrid.SetWidgetCell(inventorySlotWidget2, new Point2(l, k));
        }

        _fuelSlot.AssignInventorySlot(componentFurnace, componentFurnace.FuelSlotIndex);
        _resultSlot.AssignInventorySlot(componentFurnace, componentFurnace.ResultSlotIndex);
        _remainsSlot.AssignInventorySlot(componentFurnace, componentFurnace.RemainsSlotIndex);
    }

    public override void Update()
    {
        _fire.ParticlesPerSecond = _componentFurnace.HeatLevel > 0f ? 24 : 0;
        _progress.Value = _componentFurnace.SmeltingProgress;
        if (!_componentFurnace.IsAddedToProject)
        {
            ParentWidget?.Children.Remove(this);
        }
    }
}
