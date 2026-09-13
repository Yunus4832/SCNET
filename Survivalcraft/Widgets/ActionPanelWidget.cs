using System.Xml.Linq;

namespace Game.Widgets;

public sealed class ActionPanelWidget : CanvasWidget
{
    private const int _maximumItemsPerGroup = 4;

    private readonly CanvasWidget _primaryAccessoryHost;
    private readonly ButtonWidget[] _primaryButtons;
    private readonly object?[] _primaryItems = new object?[_maximumItemsPerGroup];
    private readonly ContainerWidget _primaryPanel;
    private readonly float[] _primaryWeights = [1f, 1f, 1f, 1f];
    private readonly CanvasWidget _primaryTrailingActionHost;
    private readonly ButtonWidget[] _secondaryButtons;
    private readonly object?[] _secondaryItems = new object?[_maximumItemsPerGroup];
    private readonly ContainerWidget _secondaryPanel;
    private readonly float[] _secondaryWeights = [1f, 1f, 1f, 1f];
    private readonly ButtonWidget _toggleButton;
    private readonly Dictionary<BevelledButtonWidget, (Color Center, Color Bevel)> _defaultButtonColors = [];
    private Func<object, bool> _itemEnabledProvider = _ => true;
    private Func<object, Color?> _itemColorProvider = _ => null;
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

    public void SetPrimaryItems(IEnumerable<object> items, IEnumerable<float>? weights = null)
    {
        SetItems(_primaryItems, items);
        SetWeights(_primaryWeights, weights);
        Refresh();
    }

    public void SetSecondaryItems(IEnumerable<object> items, IEnumerable<float>? weights = null)
    {
        SetItems(_secondaryItems, items);
        SetWeights(_secondaryWeights, weights);
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

    private static void SetWeights(float[] destination, IEnumerable<float>? weights)
    {
        Array.Fill(destination, 1f);
        if (weights is null)
        {
            return;
        }

        var source = weights.ToArray();
        if (source.Length > destination.Length)
        {
            throw new ArgumentException(
                $"An action group supports at most {destination.Length} weights.",
                nameof(weights));
        }

        if (source.Any(weight => !float.IsFinite(weight) || weight < 0f))
        {
            throw new ArgumentOutOfRangeException(nameof(weights), "Action weights must be finite and non-negative.");
        }

        Array.Copy(source, destination, source.Length);
        if (destination.All(weight => weight == 0f))
        {
            throw new ArgumentException("At least one action weight must be greater than zero.", nameof(weights));
        }
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

    private void UpdateGroupWidths(
        ButtonWidget[] buttons,
        object?[] items,
        float[] weights,
        float availableWidth,
        CanvasWidget? leadingSlot = null,
        CanvasWidget? trailingSlot = null)
    {
        var totalWeight = weights.Sum();
        var unitWidth = availableWidth / totalWeight;
        var slotIndex = 0;
        if (leadingSlot is not null)
        {
            SetSlotWidth(leadingSlot, unitWidth * GetSlotWeight(weights, slotIndex++));
        }

        for (var i = 0; i < buttons.Length; i++)
        {
            if (items[i] is not null)
            {
                SetSlotWidth(buttons[i], unitWidth * GetSlotWeight(weights, slotIndex++));
            }
        }

        if (trailingSlot is not null)
        {
            SetSlotWidth(trailingSlot, unitWidth * GetSlotWeight(weights, slotIndex));
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
            _primaryWeights,
            availableWidth,
            PrimaryAccessory is not null ? _primaryAccessoryHost : null,
            PrimaryTrailingAction is not null ? _primaryTrailingActionHost : null);
        UpdateGroupWidths(_secondaryButtons, _secondaryItems, _secondaryWeights, availableWidth);
    }

    private static void SetSlotWidth(ButtonWidget button, float width)
    {
        button.IsVisible = width > 0f;
        button.Size = new Vector2(Math.Max(width - 2f * button.Margin.X, 0f), button.Size.Y);
    }

    private static void SetSlotWidth(CanvasWidget slot, float width)
    {
        slot.IsVisible = width > 0f;
        slot.Size = new Vector2(Math.Max(width - 2f * slot.Margin.X, 0f), slot.Size.Y);
        if (slot.Children.FirstOrDefault() is CanvasWidget content)
        {
            content.Size = new Vector2(slot.Size.X, content.Size.Y);
        }
    }

    private static float GetSlotWeight(float[] weights, int slotIndex)
    {
        if (slotIndex >= weights.Length)
        {
            throw new InvalidOperationException($"An action group supports at most {weights.Length} occupied slots.");
        }

        return weights[slotIndex];
    }
}
