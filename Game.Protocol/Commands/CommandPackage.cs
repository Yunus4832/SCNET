using Game.Protocol.Identity;

using MessagePack;

namespace Game.Protocol.Commands;

[MessagePackObject]
[Union(0, typeof(CommandPermissionSnapshotPackage))]
[Union(1, typeof(CommandRequestPackage))]
[Union(2, typeof(CommandResultPackage))]
[Union(3, typeof(CommandSuggestionsPackage))]
[Union(4, typeof(CommandSuggestionsRequestPackage))]
public abstract record CommandPackage : IProtocolMessage
{
    private const int _maximumPermissionCount = 256;

    public enum CommandPackageMode : byte
    {
        Request = 0,

        Result = 1,

        PermissionSnapshot = 2,

        SuggestionRequest = 3,

        Suggestions = 4
    }

    [IgnoreMember] public abstract CommandPackageMode Mode { get; }

    [Key(0)] public string CorrelationId { get; init; } = string.Empty;

    public static CommandSuggestionsRequestPackage CreateSuggestionRequest(string input, string correlationId) =>
        new()
        {
            CorrelationId = correlationId,
            Input = input
        };

    public static CommandSuggestionsPackage CreateSuggestions(
        string input,
        string correlationId,
        IEnumerable<CommandSuggestion> suggestions
    ) => new()
    {
        CorrelationId = correlationId,
        Input = input,
        Suggestions = suggestions.Take(64).ToArray()
    };

    public static CommandRequestPackage CreateRequest(
        ResourceId commandId,
        byte[] payload,
        string? correlationId = null
    )
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new CommandRequestPackage
        {
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
            CommandId = commandId,
            Payload = payload
        };
    }

    public static CommandPermissionSnapshotPackage CreatePermissionSnapshot(
        PlayerAddress address,
        IEnumerable<CommandPermissionGrant> grants
    )
    {
        ArgumentNullException.ThrowIfNull(grants);
        return new CommandPermissionSnapshotPackage
        {
            CorrelationId = string.Empty,
            PlayerAddress = address,
            PermissionGrants = grants.Take(_maximumPermissionCount).ToArray()
        };
    }

    public static CommandResultPackage CreateResult(CommandResponse result, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new CommandResultPackage
        {
            CorrelationId = correlationId,
            Result = result
        };
    }
}
