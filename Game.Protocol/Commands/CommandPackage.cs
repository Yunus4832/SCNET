namespace Game.Protocol.Commands;

public sealed record CommandPackage : IProtocolMessage
{
    public enum CommandPackageMode : byte
    {
        Request,
        Result,
        PermissionSnapshot,
        SuggestionRequest,
        Suggestions
    }

    private const int _maximumPermissionCount = 256;

    public byte ID => (byte)PackageType.Command;

    public CommandPackageMode Mode { get; init; }

    public string CorrelationId { get; init; } = string.Empty;

    public string Input { get; init; } = string.Empty;

    public ResourceId CommandId { get; init; }

    public byte[] Payload { get; init; } = [];

    public Guid PlayerGuid { get; init; }

    public CommandResponse? Result { get; init; }

    public IReadOnlyList<CommandPermissionGrant> PermissionGrants { get; init; } = [];

    public IReadOnlyList<CommandSuggestion> Suggestions { get; init; } = [];

    public static CommandPackage CreateSuggestionRequest(string input, string correlationId) =>
        new(CommandPackageMode.SuggestionRequest, correlationId) { Input = input };

    public static CommandPackage CreateSuggestions(string input, string correlationId, IEnumerable<CommandSuggestion> suggestions) =>
        new(CommandPackageMode.Suggestions, correlationId) { Input = input, Suggestions = suggestions.Take(64).ToArray() };

    public CommandPackage()
    {
    }

    public CommandPackage(CommandPackageMode mode, string correlationId)
    {
        Mode = mode;
        CorrelationId = correlationId;
    }

    public static CommandPackage CreateRequest(
        ResourceId commandId,
        byte[] payload,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new CommandPackage(
            CommandPackageMode.Request,
            correlationId ?? Guid.NewGuid().ToString("N"))
        {
            CommandId = commandId,
            Payload = payload
        };
    }

    public static CommandPackage CreatePermissionSnapshot(
        Guid playerGuid,
        IEnumerable<CommandPermissionGrant> grants)
    {
        ArgumentNullException.ThrowIfNull(grants);
        return new CommandPackage(CommandPackageMode.PermissionSnapshot, string.Empty)
        {
            PlayerGuid = playerGuid,
            PermissionGrants = grants.Take(_maximumPermissionCount).ToArray()
        };
    }

    public static CommandPackage CreateResult(
        CommandResponse result,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new CommandPackage(CommandPackageMode.Result, correlationId)
        {
            Result = result
        };
    }

}
