using Game.Localization;

namespace Game.Commands;

public sealed record CommandSuggestion(string Value, string Description, bool IsArgument, LocalizedText? DescriptionSource = null);
