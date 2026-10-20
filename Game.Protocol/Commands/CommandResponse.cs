using System.Text.Json.Nodes;

using Game.Protocol.Messaging;

namespace Game.Protocol.Commands;

public sealed record CommandResponse(
    bool Success,
    string Code,
    string Message,
    bool IsPending = false,
    string MessageKey = "",
    IReadOnlyList<string>? MessageArguments = null,
    JsonNode? Data = null,
    GameMessagePresentation Presentation = GameMessagePresentation.None,
    bool CloseInputOnSuccess = false);
