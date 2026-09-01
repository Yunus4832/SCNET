using Game.Messaging;

namespace Game.Widgets;

/// <summary>
///     Read-only recent message history shown in the HUD.
///     Sending messages and commands belongs to <see cref="MessagePanelWidget" />.
/// </summary>
public sealed class MessageHistoryOverlayWidget : CanvasWidget
{
    private readonly GameMessageService _messageService;

    private readonly ChatTranscriptWidget _transcript = new()
    {
        Size = new Vector2(196f, 96f),
        MaximumMessages = 4,
        Padding = 6f,
        IsBackgroundVisible = false,
        MessageContentScale = 0.6f,
        NormalTextColor = new Color(220, 224, 228, 225),
        UseTextDropShadow = true,
        FadeOlderMessages = true
    };

    private int _messageCount;

    public bool DisplayEnabled
    {
        get;
        set
        {
            field = value;
            UpdateVisibility();
        }
    } = true;

    public MessageHistoryOverlayWidget(GameMessageService messageService)
    {
        _messageService = messageService;
        Size = _transcript.Size;
        ClampToBounds = true;
        IsHitTestVisible = false;
        _transcript.IsEnabled = false;
        Children.Add(_transcript);
        foreach (var message in messageService.History.TakeLast(_transcript.MaximumMessages))
        {
            _transcript.AddMessage(message);
            _messageCount++;
        }

        messageService.MessageReceived += AddMessage;
        UpdateVisibility();
    }

    public override void Dispose()
    {
        _messageService.MessageReceived -= AddMessage;
        base.Dispose();
    }

    private void AddMessage(GameMessage message)
    {
        _transcript.AddMessage(message);
        _messageCount++;
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        IsVisible = DisplayEnabled && _messageCount > 0;
    }
}
