using System.Xml.Linq;

namespace Game.Widgets;

public class CreativeInventoryPanel : CanvasWidget
{
    private int _assignedCategoryIndex = -1;

    private int _assignedPageIndex = -1;

    private readonly ComponentCreativeInventory _componentCreativeInventory;

    private readonly CreativeInventoryWidget _creativeInventoryWidget;

    private readonly GridPanelWidget _inventoryGrid;

    private int _pagesCount;

    private List<int> _slotIndices = [];

    public CreativeInventoryPanel(CreativeInventoryWidget creativeInventoryWidget)
    {
        _creativeInventoryWidget = creativeInventoryWidget;
        _componentCreativeInventory = creativeInventoryWidget.Entity.FindComponent<ComponentCreativeInventory>(true)!;
        var node = ContentManager.Get<XElement>("Widgets/CreativeInventoryPanel");
        LoadContents(this, node);
        _inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid")!;
        for (var i = 0; i < _inventoryGrid.RowsCount; i++)
        for (var j = 0; j < _inventoryGrid.ColumnsCount; j++)
        {
            var widget = new InventorySlotWidget
            {
                HideEditOverlay = true,
                HideInteractiveOverlay = true,
                HideFoodOverlay = true
            };
            _inventoryGrid.Children.Add(widget);
            _inventoryGrid.SetWidgetCell(widget, new Point2(j, i));
        }
    }

    public override void Update()
    {
        if (_assignedCategoryIndex >= 0)
        {
            if (Input.Scroll.HasValue)
            {
                var widget = HitTestGlobal(Input.Scroll.Value.XY);
                if (widget != null && widget.IsChildWidgetOf(_inventoryGrid))
                {
                    _componentCreativeInventory.PageIndex -= (int)Input.Scroll.Value.Z;
                }
            }

            if (_creativeInventoryWidget.PageDownButton.IsClicked)
            {
                _ = ++_componentCreativeInventory.PageIndex;
            }

            if (_creativeInventoryWidget.PageUpButton.IsClicked)
            {
                _ = --_componentCreativeInventory.PageIndex;
            }

            _componentCreativeInventory.PageIndex = _pagesCount > 0
                ? MathUtils.Clamp(_componentCreativeInventory.PageIndex, 0, _pagesCount - 1)
                : 0;
        }

        if (_componentCreativeInventory.CategoryIndex != _assignedCategoryIndex)
        {
            if (_creativeInventoryWidget.GetCategoryName(_componentCreativeInventory.CategoryIndex) ==
                LanguageControl.Get("CreativeInventoryWidget", 2))
            {
                _slotIndices = new List<int>(Enumerable.Range(10, _componentCreativeInventory.OpenSlotsCount - 10));
            }
            else
            {
                _slotIndices.Clear();
                for (var i = _componentCreativeInventory.OpenSlotsCount;
                     i < _componentCreativeInventory.SlotsCount;
                     i++)
                {
                    var slotValue = _componentCreativeInventory.GetSlotValue(i);
                    var num2 = Terrain.ExtractContents(slotValue);
                    if (BlocksManager.Blocks[num2].GetCategory(slotValue) ==
                        _creativeInventoryWidget.GetCategoryName(_componentCreativeInventory.CategoryIndex))
                    {
                        _slotIndices.Add(i);
                    }
                }
            }

            var num3 = _inventoryGrid.ColumnsCount * _inventoryGrid.RowsCount;
            _pagesCount = (_slotIndices.Count + num3 - 1) / num3;
            _assignedCategoryIndex = _componentCreativeInventory.CategoryIndex;
            _assignedPageIndex = -1;
            _componentCreativeInventory.PageIndex = 0;
        }

        if (_componentCreativeInventory.PageIndex != _assignedPageIndex)
        {
            var num4 = _inventoryGrid.ColumnsCount * _inventoryGrid.RowsCount;
            var num5 = _componentCreativeInventory.PageIndex * num4;
            foreach (var child in _inventoryGrid.Children)
            {
                if (child is not InventorySlotWidget inventorySlotWidget)
                {
                    continue;
                }

                if (num5 < _slotIndices.Count)
                {
                    inventorySlotWidget.AssignInventorySlot(_componentCreativeInventory, _slotIndices[num5++]);
                }
                else
                {
                    inventorySlotWidget.AssignInventorySlot(null, 0);
                }
            }

            _assignedPageIndex = _componentCreativeInventory.PageIndex;
        }

        _creativeInventoryWidget.PageLabel.Text = _pagesCount > 0
            ? $"{_componentCreativeInventory.PageIndex + 1}/{_pagesCount}"
            : string.Empty;
        _creativeInventoryWidget.PageDownButton.IsEnabled = _componentCreativeInventory.PageIndex < _pagesCount - 1;
        _creativeInventoryWidget.PageUpButton.IsEnabled = _componentCreativeInventory.PageIndex > 0;
    }
}
