namespace Game.Commands;

public sealed record CommandArgumentSuggestion(
    string Value,
    LocalizedText? Description = null
);
