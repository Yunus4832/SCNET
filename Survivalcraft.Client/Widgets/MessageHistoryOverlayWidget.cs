using Game.Messaging;

namespace Game.Widgets;

/// <summary>在 HUD 中显示最近的消息记录；通过 <see cref="MessagePanelWidget" /> 发送消息和命令。</summary>
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
        foreach (var message in messageService.History
                     .Where(message => (message.Presentation & GameMessagePresentation.Overlay) != 0)
                     .TakeLast(_transcript.MaximumMessages))
        {
            _transcript.AddMessage(message);
            _messageCount++;
        }

        messageService.OverlayRequested += AddMessage;
        UpdateVisibility();
    }

    public override void Dispose()
    {
        _messageService.OverlayRequested -= AddMessage;
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
