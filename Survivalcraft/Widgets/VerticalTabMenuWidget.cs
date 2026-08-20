using System.Xml.Linq;

using Engine.Media;

namespace Game.Widgets;

public sealed record VerticalTabMenuItem(
    string Text,
    Action Selected);

public sealed record VerticalTabMenu(
    string Icon,
    Func<IReadOnlyList<VerticalTabMenuItem>> Items,
    Vector2 ItemSize);

public sealed class VerticalTabMenuWidget : CanvasWidget
{
    private const float _itemSpacing = 2f;

    private const float _panelPadding = 4f;

    private readonly BevelledRectangleWidget _background;

    private readonly CanvasWidget _popup;

    private readonly StackPanelWidget _itemsPanel;

    private readonly StackPanelWidget _tabsPanel;

    private readonly List<TabBinding> _tabs = [];

    private readonly List<ItemBinding> _items = [];

    private TabBinding? _activeTab;

    public VerticalTabMenuWidget()
    {
        LoadContents(this, ContentManager.Get<XElement>("Widgets/VerticalTabMenuWidget"));
        _popup = Children.Find<CanvasWidget>("VerticalTabMenu.Popup")!;
        _background = Children.Find<BevelledRectangleWidget>("VerticalTabMenu.Background")!;
        _itemsPanel = Children.Find<StackPanelWidget>("VerticalTabMenu.Items")!;
        _tabsPanel = Children.Find<StackPanelWidget>("VerticalTabMenu.Tabs")!;
    }

    public void AddTab(VerticalTabMenu menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var button = CreateIconButton(menu.Icon, new Vector2(60f));
        _tabsPanel.Children.Add(button);
        _tabs.Add(new TabBinding(menu, null, button));
    }

    public void AddNavigationTab(string icon, Action selected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icon);
        ArgumentNullException.ThrowIfNull(selected);
        var button = CreateIconButton(icon, new Vector2(60f));
        _tabsPanel.Children.Add(button);
        _tabs.Add(new TabBinding(null, selected, button));
    }

    public bool IsOpen => _activeTab != null;

    public void ToggleTab(int index)
    {
        if (index < 0 || index >= _tabs.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var tab = _tabs[index];
        if (ReferenceEquals(tab, _activeTab))
        {
            Close();
        }
        else
        {
            Open(tab);
        }
    }

    public void Close()
    {
        _activeTab = null;
        _popup.IsVisible = false;
        foreach (var tab in _tabs)
        {
            tab.Button.IsChecked = false;
        }
    }

    public override void Update()
    {
        foreach (var tab in _tabs)
        {
            if (!tab.Button.IsClicked)
            {
                continue;
            }

            if (tab.Selected != null)
            {
                Close();
                tab.Selected();
            }
            else
            {
                ToggleTab(_tabs.IndexOf(tab));
            }
            return;
        }

        foreach (var item in _items)
        {
            if (!item.Button.IsClicked)
            {
                continue;
            }

            Input.Clear();
            Close();
            item.Item.Selected();
            return;
        }

        if (_activeTab != null && Input.Click.HasValue)
        {
            var clickPosition = Input.Click.Value.End;
            if (!_popup.HitTest(clickPosition) && !_tabsPanel.HitTest(clickPosition))
            {
                Close();
            }
        }
    }

    private void Open(TabBinding tab)
    {
        _activeTab = tab;
        foreach (var candidate in _tabs)
        {
            candidate.Button.IsChecked = ReferenceEquals(candidate, tab);
        }

        _items.Clear();
        _itemsPanel.Children.Clear();
        var menu = tab.Menu ?? throw new InvalidOperationException("Navigation tabs cannot be opened.");
        var menuItems = menu.Items();
        foreach (var item in menuItems)
        {
            var button = new ClickableTextRowWidget(item.Text)
            {
                Size = menu.ItemSize,
                FontScale = 0.8f
            };
            _itemsPanel.Children.Add(button);
            _items.Add(new ItemBinding(item, button));
            if (_items.Count < menuItems.Count)
            {
                _itemsPanel.Children.Add(new CanvasWidget { Size = new Vector2(0f, _itemSpacing) });
            }
        }

        var height = _panelPadding * 2f +
                     menuItems.Count * menu.ItemSize.Y +
                     Math.Max(menuItems.Count - 1, 0) * _itemSpacing;
        var popupSize = new Vector2(menu.ItemSize.X + _panelPadding * 2f, height);
        _popup.Size = popupSize;
        _background.Size = popupSize;
        _popup.IsVisible = menuItems.Count > 0;
    }

    private static BevelledButtonWidget CreateIconButton(string icon, Vector2 size)
    {
        var button = new BevelledButtonWidget
        {
            Size = size,
            IsAutoCheckingEnabled = false
        };
        button.Children.Add(new RectangleWidget
        {
            Size = new Vector2(28f),
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center,
            Subtexture = ContentManager.Get<Subtexture>(icon),
            FillColor = Color.White,
            OutlineColor = Color.Transparent,
            IsHitTestVisible = false
        });
        return button;
    }

    private sealed record TabBinding(
        VerticalTabMenu? Menu,
        Action? Selected,
        BevelledButtonWidget Button);

    private sealed record ItemBinding(
        VerticalTabMenuItem Item,
        ClickableTextRowWidget Button);
}
