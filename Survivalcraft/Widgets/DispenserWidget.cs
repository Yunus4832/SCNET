using System.Xml.Linq;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Widgets;

public class DispenserWidget : CanvasWidget
{
    private readonly CheckboxWidget _acceptsDropsBox;

    private readonly ComponentBlockEntity _componentBlockEntity;

    private readonly ComponentDispenser _componentDispenser;

    private readonly ButtonWidget _dispenseButton;

    private readonly GridPanelWidget _dispenserGrid;

    private readonly GridPanelWidget _inventoryGrid;

    private readonly ButtonWidget _shootButton;

    private readonly SubsystemTerrain _subsystemTerrain;

    public DispenserWidget(IInventory inventory, ComponentDispenser componentDispenser)
    {
        _componentDispenser = componentDispenser;
        _componentBlockEntity = componentDispenser.Entity.FindComponent<ComponentBlockEntity>(true)!;
        _subsystemTerrain = componentDispenser.Project.FindSubsystem<SubsystemTerrain>(true)!;
        var node = ContentManager.Get<XElement>("Widgets/DispenserWidget");
        LoadContents(this, node);
        _inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid")!;
        _dispenserGrid = Children.Find<GridPanelWidget>("DispenserGrid")!;
        _dispenseButton = Children.Find<ButtonWidget>("DispenseButton")!;
        _shootButton = Children.Find<ButtonWidget>("ShootButton")!;
        _acceptsDropsBox = Children.Find<CheckboxWidget>("AcceptsDropsBox")!;
        var num = 0;
        for (var i = 0; i < _dispenserGrid.RowsCount; i++)
        for (var j = 0; j < _dispenserGrid.ColumnsCount; j++)
        {
            var inventorySlotWidget = new InventorySlotWidget();
            inventorySlotWidget.AssignInventorySlot(componentDispenser, num++);
            _dispenserGrid.Children.Add(inventorySlotWidget);
            _dispenserGrid.SetWidgetCell(inventorySlotWidget, new Point2(j, i));
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
        var value = _subsystemTerrain.Terrain.GetCellValue(_componentBlockEntity.Coordinates.X,
            _componentBlockEntity.Coordinates.Y, _componentBlockEntity.Coordinates.Z);
        var data = Terrain.ExtractData(value);
        var flag = false;
        if (_dispenseButton.IsClicked)
        {
            data = DispenserBlock.SetMode(data, DispenserBlock.Mode.Dispense);
            value = Terrain.ReplaceData(value, data);
            flag = true;
            _subsystemTerrain.ChangeCell(_componentBlockEntity.Coordinates.X, _componentBlockEntity.Coordinates.Y,
                _componentBlockEntity.Coordinates.Z, value);
        }

        if (_shootButton.IsClicked)
        {
            data = DispenserBlock.SetMode(data, DispenserBlock.Mode.Shoot);
            value = Terrain.ReplaceData(value, data);
            flag = true;
            _subsystemTerrain.ChangeCell(_componentBlockEntity.Coordinates.X, _componentBlockEntity.Coordinates.Y,
                _componentBlockEntity.Coordinates.Z, value);
        }

        if (_acceptsDropsBox.IsClicked)
        {
            data = DispenserBlock.SetAcceptsDrops(data, !DispenserBlock.GetAcceptsDrops(data));
            value = Terrain.ReplaceData(value, data);
            flag = true;
            _subsystemTerrain.ChangeCell(_componentBlockEntity.Coordinates.X, _componentBlockEntity.Coordinates.Y,
                _componentBlockEntity.Coordinates.Z, value);
        }

        var mode = DispenserBlock.GetMode(data);
        if (flag && CommonLib.WorkType == WorkType.Client)
        {
            var tmp = (byte)((mode == DispenserBlock.Mode.Shoot ? 1 : 0) |
                             (DispenserBlock.GetAcceptsDrops(data) ? 1 << 1 : 0));
            var p = new DispenserPackage(_componentBlockEntity.Coordinates, tmp);
            CommonLib.Net.QueuePackage(p);
            if (CommonLib.WorkType != WorkType.Client)
            {
                p.Handle(ProjectNet.Project, CommonLib.Net, false);
            }
        }

        _dispenseButton.IsChecked = mode == DispenserBlock.Mode.Dispense;
        _shootButton.IsChecked = mode == DispenserBlock.Mode.Shoot;
        _acceptsDropsBox.IsChecked = DispenserBlock.GetAcceptsDrops(data);
        if (!_componentDispenser.IsAddedToProject)
        {
            ParentWidget?.Children.Remove(this);
        }
    }
}
