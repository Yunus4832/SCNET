using MessagePack;

namespace Game.Protocol.Messaging;

/// <summary>指定仅通过浮动提示展示，消息语气和历史记录仍按各自规则处理。</summary>
[MessagePackObject]
public sealed record GameMessageToastStyle(
    [property: Key(0)] GameMessageTone Tone,
    [property: Key(1)] bool Blinking,
    [property: Key(2)] bool PlayNotificationSound
);
