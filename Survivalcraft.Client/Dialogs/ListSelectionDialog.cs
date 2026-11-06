using System.Collections;
using System.Xml.Linq;

using Engine.Graphics;

namespace Game.Dialogs;

public class ListSelectionDialog : Dialog
{
    private readonly CanvasWidget _contentWidget;

    private readonly CanvasWidget _viewport;

    private readonly BevelledRectangleWidget _scrollThumb;

    private readonly ListPanelWidget _listWidget;

    private readonly Action<object> _selectionHandler;

    private readonly LabelWidget _titleLabelWidget;

    private double? _dismissTime;

    private bool _isDismissed;

    private bool _revealCurrentItem = true;

    private Vector2 _contentSize = new(560f, 420f);

    public ListSelectionDialog(
        string title,
        IEnumerable items,
        float itemSize,
        Func<object, Widget> itemWidgetFactory,
        Action<object> selectionHandler,
        object? currentItem = null
    )
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(itemWidgetFactory);
        ArgumentNullException.ThrowIfNull(selectionHandler);
        if (!float.IsFinite(itemSize) || itemSize <= 0f) { throw new ArgumentOutOfRangeException(nameof(itemSize)); }

        _selectionHandler = selectionHandler;
        LoadContents(this, ContentManager.Get<XElement>("Dialogs/ListSelectionDialog"));
        _titleLabelWidget = Children.Find<LabelWidget>("ListSelectionDialog.Title")!;
        _listWidget = Children.Find<ListPanelWidget>("ListSelectionDialog.List")!;
        _contentWidget = Children.Find<CanvasWidget>("ListSelectionDialog.Content")!;
        _viewport = Children.Find<CanvasWidget>("ListSelectionDialog.Viewport")!;
        _scrollThumb = Children.Find<BevelledRectangleWidget>("ListSelectionDialog.ScrollThumb")!;
        _titleLabelWidget.Text = title;
        _titleLabelWidget.IsVisible = !string.IsNullOrEmpty(title);
        _listWidget.ItemSize = itemSize;
        _listWidget.ScrollPosition = 0f;
        _listWidget.ScrollSpeed = 0f;
        _listWidget.ItemWidgetFactory = item => new SelectionItemWidget(
            itemWidgetFactory(item),
            itemSize,
            () => Equals(item, CurrentItem),
            () => Equals(item, _listWidget.SelectedItem)
        );
        foreach (var item in items) { _listWidget.AddItem(item); }

        CurrentItem = currentItem is not null && _listWidget.Items.Contains(currentItem) ? currentItem : null;
    }

    public ListSelectionDialog(
        string title,
        IEnumerable items,
        float itemSize,
        Func<object, string> itemToStringConverter,
        Action<object> selectionHandler,
        object? currentItem = null
    ) : this(title, items, itemSize, item => CreateTextItem(itemToStringConverter(item)), selectionHandler, currentItem)
    {
        ContentSize = new Vector2(480f, 420f);
    }

    public object? CurrentItem { get; }

    /// <summary>Preferred panel width and maximum list viewport height, constrained by the available space.</summary>
    public Vector2 ContentSize
    {
        get => _contentSize;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || value.X <= 0f || value.Y <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _contentSize = value;
        }
    }

    public static Widget CreateTextItem(string text, string? description = null, bool wrapDescription = false)
    {
        var content = new StackPanelWidget
        {
            Direction = LayoutDirection.Vertical,
            VerticalAlignment = WidgetAlignment.Center,
            Margin = new Vector2(0f, 6f)
        };
        content.Children.Add(
            new LabelWidget
            {
                Text = text,
                Size = new Vector2(float.PositiveInfinity, -1f),
                Color = new Color(235, 235, 235),
                Ellipsis = true,
                MaxLines = 1,
                TextAnchor = TextAnchor.HorizontalCenter
            }
        );
        if (!string.IsNullOrEmpty(description))
        {
            content.Children.Add(
                new LabelWidget
                {
                    Text = description,
                    Size = new Vector2(float.PositiveInfinity, -1f),
                    Color = new Color(160, 160, 160),
                    FontScale = 0.7f,
                    WordWrap = wrapDescription,
                    Ellipsis = true,
                    MaxLines = wrapDescription ? 3 : 1,
                    TextAnchor = TextAnchor.HorizontalCenter,
                    Margin = new Vector2(0f, 4f)
                }
            );
        }

        return content;
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        var width = MathUtils.Min(ContentSize.X, MathUtils.Max(parentAvailableSize.X - 40f, 0f));
        var header = _titleLabelWidget.IsVisible ? 40f : 0f;
        var topInset = _titleLabelWidget.IsVisible ? 4f : 12f;
        var height = MathUtils.Min(
            _listWidget.Items.Count * _listWidget.ItemSize,
            MathUtils.Min(ContentSize.Y, MathUtils.Max(parentAvailableSize.Y - 40f - header - topInset - 12f, 0f))
        );
        if (height >= _listWidget.ItemSize)
        {
            height = MathF.Floor(height / _listWidget.ItemSize) * _listWidget.ItemSize;
        }

        _contentWidget.Size = new Vector2(width, header + height + topInset + 12f);
        _titleLabelWidget.Size = new Vector2(MathUtils.Max(width - 40f, 0f), header);
        _contentWidget.SetWidgetPosition(_titleLabelWidget, new Vector2(20f, 4f));
        _viewport.Size = new Vector2(MathUtils.Max(width - 24f, 0f), height);
        _contentWidget.SetWidgetPosition(_viewport, new Vector2(12f, header + topInset));
        _listWidget.ScrollPosition = Math.Clamp(
            _listWidget.ScrollPosition,
            0f,
            MathUtils.Max(_listWidget.Items.Count * _listWidget.ItemSize - height, 0f)
        );
        base.MeasureOverride(parentAvailableSize);
    }

    public override void ArrangeOverride()
    {
        base.ArrangeOverride();
        if (_revealCurrentItem && _listWidget.ActualSize.Y > 0f)
        {
            if (CurrentItem is not null) { _listWidget.ScrollToItem(CurrentItem); }

            _revealCurrentItem = false;
        }

        var totalHeight = _listWidget.Items.Count * _listWidget.ItemSize;
        var visibleHeight = _listWidget.ActualSize.Y;
        _scrollThumb.IsVisible = visibleHeight > 0f && totalHeight > visibleHeight;
        if (_scrollThumb.IsVisible)
        {
            var thumbHeight = MathUtils.Min(
                visibleHeight,
                MathUtils.Max(24f, visibleHeight * visibleHeight / totalHeight)
            );
            _scrollThumb.Size = new Vector2(3f, thumbHeight);
            var position = _contentWidget.GetWidgetPosition(_viewport)!.Value;
            position.X = _contentWidget.ActualSize.X - 12f;
            position.Y += (visibleHeight - thumbHeight) *
                          Math.Clamp(_listWidget.ScrollPosition / (totalHeight - visibleHeight), 0f, 1f);
            _contentWidget.SetWidgetPosition(_scrollThumb, position);
        }
    }

    public override void Update()
    {
        if (Input.Back || Input.Cancel || (Input.Tap.HasValue && !_contentWidget.HitTest(Input.Tap.Value)))
        {
            Dismiss(null);
            return;
        }

        if (!_dismissTime.HasValue && _listWidget.SelectedItem is not null)
        {
            _dismissTime = Time.FrameStartTime + 0.05;
        }

        if (_dismissTime.HasValue && Time.FrameStartTime >= _dismissTime.Value) { Dismiss(_listWidget.SelectedItem); }
    }

    public void Dismiss(object? result)
    {
        if (_isDismissed) { return; }

        _isDismissed = true;
        Input.Clear();
        DialogsManager.HideDialog(this);
        if (result is not null) { _selectionHandler(result); }
    }

    private sealed class SelectionItemWidget : CanvasWidget
    {
        private readonly CanvasWidget _contentHost;

        private readonly BevelledRectangleWidget _background;

        private readonly Func<bool> _isCurrent;

        private readonly Func<bool> _isSelected;

        public SelectionItemWidget(Widget content, float height, Func<bool> isCurrent, Func<bool> isSelected)
        {
            Size = new Vector2(float.PositiveInfinity, height);
            _isCurrent = isCurrent;
            _isSelected = isSelected;
            _background = new BevelledRectangleWidget
            {
                CenterColor = new Color(30, 65, 25),
                BevelColor = new Color(30, 65, 25),
                BevelSize = 0f,
                RoundingRadius = 8f,
                RoundingCount = 6,
                DirectionalLight = 0f,
                AmbientLight = 1f,
                ShadowColor = Color.Transparent,
                Margin = new Vector2(4f, 4f)
            };
            Children.Add(_background);
            _contentHost = new CanvasWidget();
            _contentHost.Children.Add(content);
            Children.Add(_contentHost);
        }

        protected override void MeasureOverride(Vector2 parentAvailableSize)
        {
            _background.IsVisible = _isCurrent() || _isSelected();
            _contentHost.Size = new Vector2(MathUtils.Max(parentAvailableSize.X - 48f, 0f), Size.Y);
            SetWidgetPosition(_contentHost, new Vector2(24f, 0f));
            base.MeasureOverride(parentAvailableSize);
        }
    }
}
