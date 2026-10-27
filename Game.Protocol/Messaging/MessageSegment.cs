using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Messaging;

[MessagePackObject]
public sealed record MessageSegment(
    [property: Key(0)] string Text,
    [property: Key(1)] MessageTextStyle Style = MessageTextStyle.Normal,
    [property: Key(2)] Color? Color = null
);
