namespace Game.Widgets;

public sealed class ChatTranscriptWidget : CanvasWidget
{
    private readonly ResizableListWidget _messages = new()
    {
        Direction = LayoutDirection.Vertical,
        ClampToBounds = true,
        Margin = new Vector2(12f)
    };

    public event Action<RichTextAction>? ActionRequested;

    public int MaximumMessages { get; set; } = 200;

    public float Padding
    {
        get => _messages.Margin.X;
        set => _messages.Margin = new Vector2(MathUtils.Max(value, 0f));
    }

    public ChatTranscriptWidget()
    {
        ClampToBounds = true;
        Children.Add(new BevelledRectangleWidget
        {
            CenterColor = new Color(0, 0, 0, 150),
            BevelColor = new Color(255, 255, 255, 96),
            BevelSize = 1f,
            RoundingRadius = 8f
        });
        Children.Add(_messages);
        _messages.ItemWidgetFactory = CreateMessageWidget;
    }

    public void AddMessage(string message)
    {
        _messages.AddItem(message);
        while (_messages.Items.Count > MaximumMessages)
        {
            _messages.RemoveItemAt(0);
        }

        _messages.ScrollToItem(_messages.Items[^1]);
    }

    public void ClearMessages()
    {
        _messages.ClearItems();
    }

    private Widget CreateMessageWidget(object item)
    {
        var widget = new RichTextWidget
        {
            Text = item.ToString() ?? string.Empty,
            HorizontalAlignment = WidgetAlignment.Stretch
        };
        widget.ActionRequested += action => ActionRequested?.Invoke(action);
        return widget;
    }
}
