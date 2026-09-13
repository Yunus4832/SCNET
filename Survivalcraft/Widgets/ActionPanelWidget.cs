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
    private readonly Dictionary<BevelledButtonWidget, (Color Center, Color Bevel)> _defaultButtonColors = [];
    private Func<object, bool> _itemEnabledProvider = _ => true;
    private Func<object, Color?> _itemColorProvider = _ => null;
    private Func<object, string> _itemTextProvider = item => item.ToString() ?? string.Empty;
    private Func<object, float> _itemWeightProvider = _ => 1f;

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
        foreach (var button in _primaryButtons.Concat(_secondaryButtons).OfType<BevelledButtonWidget>())
        {
            _defaultButtonColors.Add(button, (button.CenterColor, button.BevelColor));
        }

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
            if (value is CanvasWidget canvasWidget)
            {
                canvasWidget.Size = new Vector2(canvasWidget.Size.X, _primaryAccessoryHost.Size.Y);
            }

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
            if (value is CanvasWidget canvasWidget)
            {
                canvasWidget.Size = new Vector2(canvasWidget.Size.X, _primaryTrailingActionHost.Size.Y);
            }

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

    public Func<object, Color?> ItemColorProvider
    {
        get => _itemColorProvider;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _itemColorProvider = value;
            Refresh();
        }
    }

    public Func<object, float> ItemWeightProvider
    {
        get => _itemWeightProvider;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _itemWeightProvider = value;
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
        UpdateLayoutWidths(ActualSize.X);
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

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        UpdateLayoutWidths(parentAvailableSize.X);
        base.MeasureOverride(parentAvailableSize);
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
            if (buttons[i] is BevelledButtonWidget bevelledButton)
            {
                var defaultColors = _defaultButtonColors[bevelledButton];
                var color = item is null ? null : _itemColorProvider(item);
                bevelledButton.CenterColor = color ?? defaultColors.Center;
                bevelledButton.BevelColor = color ?? defaultColors.Bevel;
            }

            if (item is not null)
            {
                buttons[i].Text = _itemTextProvider(item);
                buttons[i].IsEnabled = _itemEnabledProvider(item);
            }
        }
    }

    private float GetItemWeight(object item)
    {
        var weight = _itemWeightProvider(item);
        if (!float.IsFinite(weight) || weight <= 0f)
        {
            throw new InvalidOperationException("Action weights must be finite and greater than zero.");
        }

        return weight;
    }

    private void UpdateGroupWidths(
        ButtonWidget[] buttons,
        object?[] items,
        float availableWidth,
        CanvasWidget? leadingSlot = null,
        CanvasWidget? trailingSlot = null)
    {
        var usedSlots = items.Count(item => item is not null) + (leadingSlot is not null ? 1 : 0) +
                        (trailingSlot is not null ? 1 : 0);
        var totalWeight = (float)Math.Max(_maximumItemsPerGroup - usedSlots, 0);
        if (leadingSlot is not null)
        {
            totalWeight += 1f;
        }

        foreach (var item in items)
        {
            if (item is not null)
            {
                totalWeight += GetItemWeight(item);
            }
        }

        if (trailingSlot is not null)
        {
            totalWeight += 1f;
        }

        if (totalWeight <= 0f)
        {
            return;
        }

        var unitWidth = availableWidth / totalWeight;
        if (leadingSlot is not null)
        {
            SetSlotWidth(leadingSlot, unitWidth);
        }

        for (var i = 0; i < buttons.Length; i++)
        {
            if (items[i] is { } item)
            {
                SetSlotWidth(buttons[i], unitWidth * GetItemWeight(item));
            }
        }

        if (trailingSlot is not null)
        {
            SetSlotWidth(trailingSlot, unitWidth);
        }
    }

    private void UpdateLayoutWidths(float width)
    {
        if (!float.IsFinite(width) || width <= 0f)
        {
            return;
        }

        var availableWidth = Math.Max(width - _toggleButton.Size.X - 2f * _toggleButton.Margin.X, 0f);
        UpdateGroupWidths(
            _primaryButtons,
            _primaryItems,
            availableWidth,
            PrimaryAccessory is not null ? _primaryAccessoryHost : null,
            PrimaryTrailingAction is not null ? _primaryTrailingActionHost : null);
        UpdateGroupWidths(_secondaryButtons, _secondaryItems, availableWidth);
    }

    private static void SetSlotWidth(ButtonWidget button, float width)
    {
        button.Size = new Vector2(Math.Max(width - 2f * button.Margin.X, 0f), button.Size.Y);
    }

    private static void SetSlotWidth(CanvasWidget slot, float width)
    {
        slot.Size = new Vector2(Math.Max(width - 2f * slot.Margin.X, 0f), slot.Size.Y);
        if (slot.Children.FirstOrDefault() is CanvasWidget content)
        {
            content.Size = new Vector2(slot.Size.X, content.Size.Y);
        }
    }
}
