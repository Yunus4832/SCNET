namespace Game.Protocol.Messaging;

public sealed record MessageContent
{
    private readonly MessageSegment[] _segments;

    public IReadOnlyList<MessageSegment> Segments => _segments;

    public string PlainText => string.Concat(_segments.Select(segment => segment.Text));

    public MessageContent(IEnumerable<MessageSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var normalized = new List<MessageSegment>();
        foreach (var segment in segments)
        {
            ArgumentNullException.ThrowIfNull(segment);
            if (string.IsNullOrEmpty(segment.Text))
            {
                continue;
            }

            if (normalized.LastOrDefault() is { } previous &&
                previous.Style == segment.Style &&
                previous.Color == segment.Color)
            {
                normalized[^1] = previous with { Text = previous.Text + segment.Text };
            }
            else
            {
                normalized.Add(segment);
            }
        }

        _segments = normalized.ToArray();
    }

    public static MessageContent Plain(string text) =>
        new([new MessageSegment(text ?? string.Empty)]);
}
