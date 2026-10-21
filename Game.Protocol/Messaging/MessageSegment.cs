using Engine.Core;

namespace Game.Protocol.Messaging;

public sealed record MessageSegment(
    string Text,
    MessageTextStyle Style = MessageTextStyle.Normal,
    Color? Color = null
);
