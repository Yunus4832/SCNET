using Game.Protocol.Localization;

namespace Game.Protocol.Commands;

public sealed record CommandSuggestion(
    string Value,
    string Description,
    bool IsArgument,
    LocalizedText? DescriptionSource = null
);
