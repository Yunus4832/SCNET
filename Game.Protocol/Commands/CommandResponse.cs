using System.Text.Json.Nodes;

using Game.Protocol.Messaging;

using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Commands;

[MessagePackObject]
public sealed record CommandResponse(
    [property: Key(0)] bool Success,
    [property: Key(1)] string Code,
    [property: Key(2)] string Message,
    [property: Key(3)] bool IsPending = false,
    [property: Key(4)] string MessageKey = "",
    [property: Key(5), MaxLength(32)] IReadOnlyList<string>? MessageArguments = null,
    [property: Key(6)] JsonNode? Data = null,
    [property: Key(7)] GameMessagePresentation Presentation = GameMessagePresentation.None,
    [property: Key(8)] bool CloseInputOnSuccess = false
);
