using Game.Messaging;

namespace Game.Widgets;

public sealed class ChatTranscriptWidget : CanvasWidget
{
    private sealed class TranscriptEntry(GameMessage message)
    {
        public GameMessage Message { get; } = message;

        public float Opacity { get; set; } = 1f;
    }

    private readonly BevelledRectangleWidget _background = new()
    {
        CenterColor = new Color(0, 0, 0, 150),
        BevelColor = new Color(255, 255, 255, 96),
        BevelSize = 1f,
        RoundingRadius = 8f
    };

    private readonly ResizableListWidget _messages = new()
    {
        Direction = LayoutDirection.Vertical,
        ClampToBounds = true,
        Margin = new Vector2(12f)
    };

    public int MaximumMessages { get; set; } = 200;

    public bool IsBackgroundVisible
    {
        get => _background.IsVisible;
        set => _background.IsVisible = value;
    }

    public float MessageContentScale { get; set; } = 1f;

    public Color NormalTextColor { get; set; } = Color.White;

    public bool UseTextDropShadow { get; set; }

    public bool FadeOlderMessages { get; set; }

    public float Padding
    {
        get => _messages.Margin.X;
        set => _messages.Margin = new Vector2(MathUtils.Max(value, 0f));
    }

    public ChatTranscriptWidget()
    {
        ClampToBounds = true;
        Children.Add(_background);
        Children.Add(_messages);
        _messages.ItemWidgetFactory = CreateMessageWidget;
    }

    public void AddMessage(GameMessage message)
    {
        var entry = new TranscriptEntry(message);
        _messages.AddItem(entry);
        while (_messages.Items.Count > MaximumMessages)
        {
            _messages.RemoveItemAt(0);
        }

        if (FadeOlderMessages)
        {
            RebuildMessageOpacity();
        }

        _messages.ScrollToItem(entry);
    }

    private Widget CreateMessageWidget(object item)
    {
        var entry = (TranscriptEntry)item;
        var widget = new RichTextWidget
        {
            Content = GameMessageFormatter.Format(entry.Message),
            ContentScale = MessageContentScale,
            NormalTextColor = NormalTextColor,
            UseDropShadow = UseTextDropShadow,
            ColorTransform = new Color(1f, 1f, 1f, entry.Opacity),
            HorizontalAlignment = WidgetAlignment.Stretch
        };
        return widget;
    }

    private void RebuildMessageOpacity()
    {
        var entries = _messages.Items.Cast<TranscriptEntry>().ToArray();
        for (var i = 0; i < entries.Length; i++)
        {
            var age = entries.Length - 1 - i;
            entries[i].Opacity = MathUtils.Max(1f - 0.12f * age, 0.4f);
        }

        _messages.ClearItems();
        foreach (var entry in entries)
        {
            _messages.AddItem(entry);
        }
    }
}
