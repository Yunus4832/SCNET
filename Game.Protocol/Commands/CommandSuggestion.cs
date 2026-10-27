using Game.Protocol.Localization;

using MessagePack;

namespace Game.Protocol.Commands;

[MessagePackObject]
public sealed record CommandSuggestion(
    [property: Key(0)] string Value,
    [property: Key(1)] string Description,
    [property: Key(2)] bool IsArgument,
    [property: Key(3)] LocalizedText? DescriptionSource = null
);
