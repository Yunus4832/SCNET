using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Commands;

[MessagePackObject]
public sealed record CommandSuggestionsPackage : CommandPackage
{
    [IgnoreMember] public override CommandPackageMode Mode => CommandPackageMode.Suggestions;

    [Key(1), MaxLength(64)] public string Input { get; init; } = string.Empty;

    [Key(2)] public IReadOnlyList<CommandSuggestion> Suggestions { get; init; } = [];
}
