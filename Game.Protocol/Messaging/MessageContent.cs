namespace Game.Protocol.Messaging;

[MessagePack.MessagePackFormatter(typeof(Serialization.Formatters.ProtocolValueFormatter))]
public sealed record MessageContent
{
    private readonly MessageSegment[] _segments;

    public IReadOnlyList<MessageSegment> Segments => _segments;

    public string PlainText => string.Concat(
        _segments.Select(segment =>
            segment.Text.IsLiteral
                ? segment.Text.Fallback
                : throw new InvalidOperationException("Only literal text can be flattened without a locale.")
        )
    );

    public MessageContent(IEnumerable<MessageSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var normalized = new List<MessageSegment>();
        foreach (var segment in segments)
        {
            ArgumentNullException.ThrowIfNull(segment);
            if (segment.Text.IsLiteral && string.IsNullOrEmpty(segment.Text.Fallback))
            {
                continue;
            }

            if (normalized.LastOrDefault() is { } previous &&
                previous.Style == segment.Style &&
                previous.Color == segment.Color &&
                previous.Text.IsLiteral &&
                segment.Text.IsLiteral)
            {
                normalized[^1] = previous with
                {
                    Text = Localization.LocalizedText.Literal(previous.Text.Fallback + segment.Text.Fallback)
                };
            }
            else
            {
                normalized.Add(segment);
            }
        }

        _segments = normalized.ToArray();
    }

    public static MessageContent Plain(string text) =>
        new([new MessageSegment(text)]);
}
