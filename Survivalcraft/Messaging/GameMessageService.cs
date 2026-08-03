using EntitySystem.Core;

using Game.Network;
using Game.Network.Packages;

namespace Game.Messaging;

public sealed class GameMessageService(Project project)
{
    public const int MaximumHistoryCount = 200;

    private readonly Queue<GameMessage> _history = new();

    private readonly Project _project = project ?? throw new ArgumentNullException(nameof(project));

    public IReadOnlyList<GameMessage> History => [.. _history];

    public event Action<GameMessage>? MessageReceived;

    public event Action<GameMessage>? ToastRequested;

    public void Publish(
        GameMessage message,
        IEnumerable<byte>? recipients = null,
        bool includePublisher = true)
    {
        ArgumentNullException.ThrowIfNull(message);
        var normalizedMessage = Normalize(message);
        var recipientList = recipients?.Distinct().ToArray();
        QueueMessage(normalizedMessage, recipientList);
        if (includePublisher && (CommonLib.Net.IsServer || !CommonLib.Net.IsConnected))
        {
            Insert(normalizedMessage);
        }
    }

    internal void Receive(
        GameMessage message,
        bool external = false)
    {
        ArgumentNullException.ThrowIfNull(message);
        Insert(message, external);
    }

    public void DisplayLocal(GameMessage message, bool external = false)
    {
        ArgumentNullException.ThrowIfNull(message);
        Insert(message, external);
    }

    internal void Relay(
        GameMessage message,
        IReadOnlyCollection<byte>? recipients,
        Client? except = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        QueueMessage(message, recipients, except);
    }

    internal GameMessage Normalize(GameMessage message)
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

    private void Insert(GameMessage message, bool external = false)
    {
        message = message.ResolveLocalization();
        if ((message.Presentation & GameMessagePresentation.History) != 0)
        {
            while (_history.Count >= MaximumHistoryCount)
            {
                _history.Dequeue();
            }

            _history.Enqueue(message);
        }

        if (!external)
        {
            Log.Information(GameMessageFormatter.Format(message).PlainText);
        }

        if ((message.Presentation & GameMessagePresentation.Overlay) != 0)
        {
            MessageReceived?.Invoke(message);
        }

        if ((message.Presentation & GameMessagePresentation.Toast) != 0)
        {
            ToastRequested?.Invoke(message);
        }
    }

    private static void QueueMessage(
        GameMessage message,
        IReadOnlyCollection<byte>? recipients,
        Client? except = null)
    {
        var netNode = CommonLib.Net;
        if (!netNode.IsServer || recipients is null)
        {
            netNode.QueuePackage(new MessagePackage(message)
            {
                Except = except
            });
            return;
        }

        foreach (var clientId in recipients.Distinct())
        {
            if (!netNode.Clients.TryGetValue(clientId, out var client) ||
                client == netNode.Self ||
                client == except)
            {
                continue;
            }

            netNode.QueuePackage(new MessagePackage(message)
            {
                To = client
            });
        }
    }
}
