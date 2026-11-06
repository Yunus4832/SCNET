using MessagePack;

namespace Game.Protocol.Messaging;

/// <summary>Explicit toast-only presentation. Message tone and history retain their independent meaning.</summary>
[MessagePackObject]
public sealed record GameMessageToastStyle(
    [property: Key(0)] GameMessageTone Tone,
    [property: Key(1)] bool Blinking,
    [property: Key(2)] bool PlayNotificationSound
);
