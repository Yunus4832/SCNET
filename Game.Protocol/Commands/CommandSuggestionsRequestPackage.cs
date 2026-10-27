using MessagePack;

namespace Game.Protocol.Commands;

[MessagePackObject]
public sealed record CommandSuggestionsRequestPackage : CommandPackage
{
    [IgnoreMember] public override CommandPackageMode Mode => CommandPackageMode.SuggestionRequest;

    [Key(1)] public string Input { get; init; } = string.Empty;
}
