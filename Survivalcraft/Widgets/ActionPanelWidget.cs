using System.Xml.Linq;

namespace Game.Widgets;

public sealed class ActionPanelWidget : CanvasWidget
{
    private const int _maximumItemsPerGroup = 4;

    private readonly CanvasWidget _primaryAccessoryHost;
    private readonly ButtonWidget[] _primaryButtons;
    private readonly object?[] _primaryItems = new object?[_maximumItemsPerGroup];
    private readonly ContainerWidget _primaryPanel;
    private readonly CanvasWidget _primaryTrailingActionHost;
    private readonly ButtonWidget[] _secondaryButtons;
    private readonly object?[] _secondaryItems = new object?[_maximumItemsPerGroup];
    private readonly ContainerWidget _secondaryPanel;
    private readonly ButtonWidget _toggleButton;
    private Func<object, bool> _itemEnabledProvider = _ => true;
    private Func<object, string> _itemTextProvider = item => item.ToString() ?? string.Empty;

    public ActionPanelWidget()
    {
        LoadContents(this, ContentManager.Get<XElement>("Widgets/ActionPanelWidget"));
        _primaryPanel = Children.Find<ContainerWidget>("ActionPanel.Primary")!;
        _primaryAccessoryHost = Children.Find<CanvasWidget>("ActionPanel.PrimaryAccessory")!;
        _primaryTrailingActionHost = Children.Find<CanvasWidget>("ActionPanel.PrimaryTrailingAction")!;
        _secondaryPanel = Children.Find<ContainerWidget>("ActionPanel.Secondary")!;
        _toggleButton = Children.Find<ButtonWidget>("ActionPanel.Toggle")!;
        _primaryButtons = FindButtons("ActionPanel.Primary", _primaryPanel);
        _secondaryButtons = FindButtons("ActionPanel.Secondary", _secondaryPanel);
        Refresh();
    }

    public event Action<object>? ItemClicked;

    public string PrimaryToggleText { get; set; } = "...";

    public string SecondaryToggleText { get; set; } = "<";

    public bool IsSecondaryVisible { get; private set; }

    public Widget? PrimaryAccessory
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            _primaryAccessoryHost.Children.Clear();
            field = value;
            if (value is not null)
            {
                _primaryAccessoryHost.Children.Add(value);
            }

            _primaryAccessoryHost.IsVisible = value is not null;
        }
    }

    public Widget? PrimaryTrailingAction
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            _primaryTrailingActionHost.Children.Clear();
            field = value;
            if (value is not null)
            {
                _primaryTrailingActionHost.Children.Add(value);
            }

            _primaryTrailingActionHost.IsVisible = value is not null && !IsSecondaryVisible;
        }
    }

    public Func<object, string> ItemTextProvider
    {
        get => _itemTextProvider;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _itemTextProvider = value;
            Refresh();
        }
    }

    public Func<object, bool> ItemEnabledProvider
    {
        get => _itemEnabledProvider;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _itemEnabledProvider = value;
            Refresh();
        }
    }

    public void SetPrimaryItems(IEnumerable<object> items)
    {
        SetItems(_primaryItems, items);
        Refresh();
    }

    public void SetSecondaryItems(IEnumerable<object> items)
    {
        SetItems(_secondaryItems, items);
        if (_secondaryItems.All(item => item is null))
        {
            IsSecondaryVisible = false;
        }

        Refresh();
    }

    public void ShowPrimaryItems()
    {
        IsSecondaryVisible = false;
        Refresh();
    }

    public void Refresh()
    {
        _primaryPanel.IsVisible = !IsSecondaryVisible;
        _primaryTrailingActionHost.IsVisible = PrimaryTrailingAction is not null && !IsSecondaryVisible;
        _secondaryPanel.IsVisible = IsSecondaryVisible;
        _toggleButton.IsEnabled = _secondaryItems.Any(item => item is not null);
        _toggleButton.Text = IsSecondaryVisible ? SecondaryToggleText : PrimaryToggleText;
        RefreshButtons(_primaryButtons, _primaryItems);
        RefreshButtons(_secondaryButtons, _secondaryItems);
    }

    public override void Update()
    {
        Refresh();
        if (_toggleButton.IsClicked)
        {
            IsSecondaryVisible = !IsSecondaryVisible;
            Refresh();
            return;
        }

        var buttons = IsSecondaryVisible ? _secondaryButtons : _primaryButtons;
        var items = IsSecondaryVisible ? _secondaryItems : _primaryItems;
        for (var i = 0; i < buttons.Length; i++)
        {
            if (items[i] is { } item && buttons[i].IsClicked)
            {
                ItemClicked?.Invoke(item);
                return;
            }
        }
    }

    private static ButtonWidget[] FindButtons(string prefix, ContainerWidget panel)
    {
        return Enumerable.Range(1, _maximumItemsPerGroup)
            .Select(index => panel.Children.Find<ButtonWidget>($"{prefix}.{index}")!)
            .ToArray();
    }

    private static void SetItems(object?[] destination, IEnumerable<object> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var source = items.ToArray();
        if (source.Length > destination.Length)
        {
            throw new ArgumentException($"An action group supports at most {destination.Length} items.", nameof(items));
        }

        Array.Clear(destination);
        Array.Copy(source, destination, source.Length);
    }

    private void RefreshButtons(ButtonWidget[] buttons, object?[] items)
    {
        for (var i = 0; i < buttons.Length; i++)
        {
            var item = items[i];
            buttons[i].IsVisible = item is not null;
            if (item is not null)
            {
                buttons[i].Text = _itemTextProvider(item);
                buttons[i].IsEnabled = _itemEnabledProvider(item);
            }
        }
    }
}
