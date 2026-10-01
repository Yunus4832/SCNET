using System.Xml.Linq;

namespace Game.Widgets;

public class ShortInventoryWidget : CanvasWidget
{
    private const float _horizontalMargin = 25f;

    private const float _pageButtonWidth = 44f;

    private const float _sideControlsReservedWidth = 320f;

    private int _assignedPageIndex = -1;

    private IInventory? _inventory;

    private readonly GridPanelWidget _inventoryGrid;

    private readonly ButtonWidget _nextPageButton;

    private readonly PageIndicatorWidget _pageIndicator;

    private readonly ButtonWidget _previousPageButton;

    public bool AreSideControlsVisible { get; set; }

    public float HorizontalSafeAreaPadding { get; set; }

    public ShortInventoryWidget()
    {
        var node = ContentManager.Get<XElement>("Widgets/ShortInventoryWidget");
        LoadContents(this, node);
        _inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid")!;
        _previousPageButton = Children.Find<ButtonWidget>("PreviousPageButton")!;
        _nextPageButton = Children.Find<ButtonWidget>("NextPageButton")!;
        _pageIndicator = Children.Find<PageIndicatorWidget>("PageIndicator")!;
    }

    public void AssignComponents(IInventory? inventory)
    {
        if (inventory == _inventory)
        {
            return;
        }

        _inventory = inventory;
        _inventoryGrid.Children.Clear();
        _assignedPageIndex = -1;
    }

    public override void Update()
    {
        if (_inventory is not IPagedInventory pagedInventory)
        {
            _pageIndicator.IsVisible = false;
            _previousPageButton.IsVisible = false;
            _nextPageButton.IsVisible = false;
            return;
        }

        if (_previousPageButton.IsClicked)
        {
            pagedInventory.ChangeHotbarPage(-1);
        }

        if (_nextPageButton.IsClicked)
        {
            pagedInventory.ChangeHotbarPage(1);
        }

        var pagesCount = pagedInventory.GetHotbarPagesCount();
        var pageIndex = pagedInventory.GetHotbarPageIndex();
        _pageIndicator.IsVisible = true;
        _pageIndicator.PageCount = pagesCount;
        _pageIndicator.PageIndex = pageIndex;
        _previousPageButton.IsVisible = true;
        _nextPageButton.IsVisible = true;
        _previousPageButton.IsEnabled = pageIndex > 0;
        _nextPageButton.IsEnabled = pageIndex < pagesCount - 1;
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        if (_inventory == null)
        {
            return;
        }

        var controlsWidth = AreSideControlsVisible ? _sideControlsReservedWidth : 0f;
        var availableWidth = parentAvailableSize.X - controlsWidth - _horizontalMargin -
                             2f * HorizontalSafeAreaPadding;
        var pagedInventory = _inventory as IPagedInventory;
        if (pagedInventory != null)
        {
            availableWidth -= 2f * _pageButtonWidth;
            _inventory.VisibleSlotsCount = MathUtils.Clamp((int)(availableWidth / 72f), 6,
                pagedInventory.HotbarSlotsCount);
        }

        var pageIndex = pagedInventory?.GetHotbarPageIndex() ?? 0;
        if (_inventory.VisibleSlotsCount != _inventoryGrid.Children.Count || pageIndex != _assignedPageIndex)
        {
            _inventoryGrid.Children.Clear();
            _inventoryGrid.RowsCount = 1;
            _inventoryGrid.ColumnsCount = _inventory.VisibleSlotsCount;
            for (var i = 0; i < _inventoryGrid.ColumnsCount; i++)
            {
                var inventorySlotWidget = new InventorySlotWidget
                {
                    BevelColor = new Color(181, 172, 154) * 0.6f,
                    CenterColor = new Color(181, 172, 154) * 0.33f
                };
                var slotIndex = pagedInventory?.GetHotbarSlotIndex(i) ?? i;
                if (slotIndex >= 0)
                {
                    inventorySlotWidget.AssignInventorySlot(_inventory, slotIndex);
                }
                else
                {
                    inventorySlotWidget.ShowEmptyPlaceholder = true;
                    inventorySlotWidget.IsEnabled = false;
                    inventorySlotWidget.BevelColor = new Color(181, 172, 154) * 0.18f;
                    inventorySlotWidget.CenterColor = new Color(181, 172, 154) * 0.08f;
                    inventorySlotWidget.AssignInventorySlot(null, 0);
                }

                _inventoryGrid.Children.Add(inventorySlotWidget);
                _inventoryGrid.SetWidgetCell(inventorySlotWidget, new Point2(i, 0));
            }

            _assignedPageIndex = pageIndex;
        }

        base.MeasureOverride(parentAvailableSize);
    }
}
