using System.Text.Json.Nodes;

using Game.Protocol.Localization;
using Game.Protocol.Messaging;

using MessagePack;

namespace Game.Protocol.Commands;

[MessagePackObject]
public sealed record CommandResponse(
    [property: Key(0)] bool Success,
    [property: Key(1)] string Code,
    [property: Key(2)] LocalizedText Message,
    [property: Key(3)] bool IsPending = false,
    [property: Key(4)] JsonNode? Data = null,
    [property: Key(5)] GameMessagePresentation Presentation = GameMessagePresentation.None,
    [property: Key(6)] bool CloseInputOnSuccess = false
);
