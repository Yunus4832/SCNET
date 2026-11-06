using Engine.Core.Mathematics;

using Game.Protocol.Localization;

using MessagePack;

namespace Game.Protocol.Messaging;

[MessagePackObject]
[method: SerializationConstructor]
public sealed record MessageSegment(
    [property: Key(0)] LocalizedText Text,
    [property: Key(1)] MessageTextStyle Style = MessageTextStyle.Normal,
    [property: Key(2)] Color? Color = null
)
{
    public MessageSegment(string literal, MessageTextStyle style = MessageTextStyle.Normal, Color? color = null)
        : this(LocalizedText.Literal(literal), style, color)
    {
    }
}
