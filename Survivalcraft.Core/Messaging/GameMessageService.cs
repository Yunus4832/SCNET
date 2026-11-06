using EntitySystem.Core;

namespace Game.Messaging;

public sealed class GameMessageService(Project project)
{
    public const int MaximumHistoryCount = 200;

    private readonly Queue<GameMessage> _history = new();

    private readonly Project _project = project ?? throw new ArgumentNullException(nameof(project));

    public IReadOnlyList<GameMessage> History => [.. _history];

    public event Action<GameMessage>? HistoryMessageAdded;

    public event Action<GameMessage>? OverlayRequested;

    public event Action<GameMessage>? ToastRequested;

    public void Publish(
        GameMessage message,
        IEnumerable<byte>? recipients = null,
        bool includePublisher = true
    )
    {
        ArgumentNullException.ThrowIfNull(message);
        if (_project.IsDisposed) { return; }

        var runtime = WorldRuntimeContext.For(_project);
        if (runtime.IsClosed) { return; }

        var normalizedMessage = Normalize(message);
        var recipientList = recipients?.Distinct().ToArray();
        QueueMessage(normalizedMessage, recipientList);
        if (includePublisher && runtime.IsAuthority)
        {
            Insert(normalizedMessage);
        }
    }

    public void Receive(GameMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Insert(message);
    }

    public void DisplayLocal(GameMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Insert(message);
    }

    public void Relay(
        GameMessage message,
        IReadOnlyCollection<byte>? recipients,
        WorldConnection? except = null
    )
    {
        ArgumentNullException.ThrowIfNull(message);
        QueueMessage(message, recipients, except);
    }

    public GameMessage Normalize(GameMessage message)
    {
        if (message.Kind is not GameMessageKind.Chat)
        {
            return message;
        }

        var text = message.Content.PlainText;
        var blockedKeywords = _project.FindSubsystem<SubsystemGameInfo>(true)!
            .WorldSettings.KeywordBlocking
            .Split([';'], StringSplitOptions.RemoveEmptyEntries);
        text = blockedKeywords.Aggregate(text, (current, keyword) => current.Replace(keyword, "*"));

        return message with { Content = MessageContent.Plain(text) };
    }

    private void Insert(GameMessage message)
    {
        if ((message.Presentation & GameMessagePresentation.History) != 0)
        {
            while (_history.Count >= MaximumHistoryCount)
            {
                _history.Dequeue();
            }

            _history.Enqueue(message);
            HistoryMessageAdded?.Invoke(message);
        }

        if ((message.Presentation & GameMessagePresentation.Overlay) != 0)
        {
            OverlayRequested?.Invoke(message);
        }

        if ((message.Presentation & GameMessagePresentation.Toast) != 0)
        {
            ToastRequested?.Invoke(message);
        }
    }

    private void QueueMessage(
        GameMessage message,
        IReadOnlyCollection<byte>? recipients,
        WorldConnection? except = null
    )
    {
        if (_project.IsDisposed) { return; }

        var runtime = WorldRuntimeContext.For(_project);
        if (runtime.IsClosed) { return; }

        var connections = WorldConnections.For(runtime);
        if (!runtime.IsAuthority)
        {
            NetworkSender.SendToServer(runtime, new MessagePackage(message));
            return;
        }

        if (recipients is null)
        {
            var audience = except is null ? PackageAudience.Global : PackageAudience.Except(except);
            NetworkSender.Send(_project, new MessagePackage(message), audience);
            return;
        }

        foreach (var clientId in recipients.Distinct())
        {
            if (!connections.Members.TryGetValue(clientId, out var client) ||
                client == connections.Self ||
                client == except)
            {
                continue;
            }

            NetworkSender.Send(_project, new MessagePackage(message), PackageAudience.To(client));
        }
    }
}
