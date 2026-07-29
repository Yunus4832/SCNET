using EntitySystem.Core;

using Game.Localization;

namespace Game.Commands;

public enum CommandSource
{
    Local,
    Player,
    ServerConsole,
    Mod,
    HttpApi,
    DebugApi
}

public enum CommandArgumentKind
{
    String,
    Integer,
    Number,
    Boolean,
    Guid
}

public enum CommandExecutionEnvironment
{
    Any,
    Server,
    HeadlessServer
}

public enum CommandSourcePolicy
{
    Any,
    LocalOnly,
    PlayerOnly,
    ServerConsoleOnly,
    HttpApiOnly
}

public enum CommandGrantPolicy
{
    Standard,
    Protected,
    NonGrantable
}

public sealed class CommandPrincipal
{
    private readonly HashSet<string> _delegablePermissions;

    private readonly HashSet<string> _permissions;

    public string Name { get; }

    public PlayerData? Player { get; }

    public IReadOnlySet<string> Permissions => _permissions;

    public CommandPrincipal(
        string name,
        PlayerData? player = null,
        IEnumerable<string>? permissions = null,
        IEnumerable<string>? delegablePermissions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Player = player;
        _permissions = new HashSet<string>(
            (permissions ?? []).Select(CommandPermissionSet.Normalize),
            StringComparer.OrdinalIgnoreCase);
        _delegablePermissions = new HashSet<string>(
            (delegablePermissions ?? []).Select(CommandPermissionSet.Normalize),
            StringComparer.OrdinalIgnoreCase);
    }

    public bool HasPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return true;
        }

        var normalized = CommandPermissionSet.Normalize(permission);
        if (normalized == CommandPermissionSet.GrantPermission)
        {
            return _delegablePermissions.Count > 0 ||
                   _permissions.Contains(CommandPermissionSet.ManageStandardPermission);
        }

        return _permissions.Any(granted => CommandPermissionSet.Implies(granted, normalized));
    }

    public bool CanDelegate(string permission)
    {
        var normalized = CommandPermissionSet.Normalize(permission);
        return _delegablePermissions.Any(granted =>
            CommandPermissionSet.Implies(granted, normalized));
    }

    public static CommandPrincipal FromPlayer(PlayerData player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return new CommandPrincipal(
            player.Name,
            player,
            player.CommandPermissions.Grants.Select(grant => grant.Permission),
            player.CommandPermissions.Grants
                .Where(grant => grant.CanDelegate)
                .Select(grant => grant.Permission));
    }

    public static CommandPrincipal ServerConsole { get; } =
        new("Server", permissions: ["*"], delegablePermissions: ["*"]);

    public static CommandPrincipal Local { get; } =
        new("Local");

    public static CommandPrincipal LocalHost { get; } =
        new("LocalHost", permissions: ["server.run_mode.set"]);
}

public sealed class CommandContext(
    CommandSource source,
    CommandPrincipal principal,
    Project? project,
    string? correlationId = null
)
{
    public CommandSource Source { get; } = source;

    public CommandPrincipal Principal { get; } = principal ?? throw new ArgumentNullException(nameof(principal));

    public Project? Project { get; } = project;

    public string CorrelationId { get; } = string.IsNullOrWhiteSpace(correlationId)
        ? Guid.NewGuid().ToString("N")
        : correlationId;

    public CommandRegistry Registry { get; internal set; } = null!;
}

public sealed record CommandResult(
    bool Success,
    string Code,
    string Message,
    bool Sensitive = false,
    CommandResultAudience Audience = CommandResultAudience.Requester,
    CommandResultState State = CommandResultState.Completed,
    CommandResultPresentation Presentation = CommandResultPresentation.Default,
    string MessageKey = "",
    IReadOnlyList<string>? MessageArguments = null)
{
    public static CommandResult Ok(string message, string code = "command.ok") => new(true, code, message);

    public static CommandResult PublicOk(string message, string code = "command.ok") =>
        new(true, code, message, Audience: CommandResultAudience.AllPlayers);

    public static CommandResult SensitiveOk(string message, string code = "command.ok") =>
        new(true, code, message, true);

    public static CommandResult Pending(string message, string code = "command.pending") =>
        new(true, code, message, State: CommandResultState.Pending);

    public static CommandResult SilentOk(string code = "command.ok") =>
        new(true, code, string.Empty, Presentation: CommandResultPresentation.Silent);

    public static CommandResult Fail(string code, string message) => new(false, code, message);

    public static CommandResult LocalizedOk(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments) =>
        new(
            true,
            code,
            FormatFallback(fallback, arguments),
            MessageKey: messageKey,
            MessageArguments: arguments);

    public static CommandResult LocalizedPublicOk(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments) =>
        new(
            true,
            code,
            FormatFallback(fallback, arguments),
            Audience: CommandResultAudience.AllPlayers,
            MessageKey: messageKey,
            MessageArguments: arguments);

    public static CommandResult LocalizedSensitiveOk(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments) =>
        new(
            true,
            code,
            FormatFallback(fallback, arguments),
            Sensitive: true,
            MessageKey: messageKey,
            MessageArguments: arguments);

    public static CommandResult LocalizedPending(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments) =>
        new(
            true,
            code,
            FormatFallback(fallback, arguments),
            State: CommandResultState.Pending,
            MessageKey: messageKey,
            MessageArguments: arguments);

    public static CommandResult LocalizedFail(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments) =>
        new(
            false,
            code,
            FormatFallback(fallback, arguments),
            MessageKey: messageKey,
            MessageArguments: arguments);

    private static string FormatFallback(string fallback, string[] arguments)
    {
        return arguments.Length == 0
            ? fallback
            : string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                fallback,
                arguments);
    }
}

public enum CommandResultState
{
    Completed,
    Pending
}

public enum CommandResultAudience
{
    Requester,
    AllPlayers
}

public enum CommandResultPresentation
{
    Default,
    Silent
}

public sealed class CommandArguments
{
    private readonly Dictionary<string, object> _values;

    internal CommandArguments(Dictionary<string, object> values)
    {
        _values = values;
    }

    public T Get<T>(string name)
    {
        if (!_values.TryGetValue(name, out var value))
        {
            throw new KeyNotFoundException($"Command argument \"{name}\" was not supplied.");
        }

        return (T)value;
    }

    public bool TryGet<T>(string name, out T? value)
    {
        if (_values.TryGetValue(name, out var stored) && stored is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }
}

public abstract record CommandSegment;

public sealed record CommandLiteral(string Value) : CommandSegment
{
    public string Value { get; } = Validate(Value);

    private static string Validate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Any(char.IsWhiteSpace)
            ? throw new ArgumentException("Command literals cannot contain whitespace.", nameof(value))
            : value;
    }
}

public sealed record CommandArgument(
    string Name,
    CommandArgumentKind Kind = CommandArgumentKind.String,
    IReadOnlyList<string>? Choices = null,
    Func<CommandSuggestionContext, IEnumerable<CommandArgumentSuggestion>>? SuggestionProvider = null
) : CommandSegment
{
    public string Name { get; } = Validate(Name);

    private static string Validate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Any(char.IsWhiteSpace)
            ? throw new ArgumentException("Command argument names cannot contain whitespace.", nameof(value))
            : value;
    }
}

public sealed record CommandArgumentSuggestion(
    string Value,
    LocalizedText? Description = null);

public sealed class CommandSuggestionContext(
    CommandRegistry registry,
    CommandPrincipal principal,
    CommandSource source,
    IReadOnlyList<string> completedTokens)
{
    public CommandRegistry Registry { get; } = registry;

    public CommandPrincipal Principal { get; } = principal;

    public CommandSource Source { get; } = source;

    public IReadOnlyList<string> CompletedTokens { get; } = completedTokens;

    public Project? Project => Principal.Player?.Project ?? GameManager.Project;
}

/// <summary>
/// Describes one textual route and converts parsed arguments into a typed command.
/// </summary>
public sealed class CommandRoute(
    IEnumerable<CommandSegment> segments,
    Type commandType,
    Func<CommandArguments, IGameCommand> createCommand,
    LocalizedText? description = null
)
{
    public IReadOnlyList<CommandSegment> Segments { get; } =
        segments?.ToArray() ?? throw new ArgumentNullException(nameof(segments));

    public LocalizedText Description { get; } =
        description ?? LocalizedText.Empty;

    public Type CommandType { get; } =
        commandType ?? throw new ArgumentNullException(nameof(commandType));

    public Func<CommandArguments, IGameCommand> CreateCommand { get; } =
        createCommand ?? throw new ArgumentNullException(nameof(createCommand));

}

/// <summary>
/// Declares textual names and routes for the text frontend. This is a binding,
/// not an executable command definition.
/// </summary>
public sealed class TextCommand : ICommandAdapterBinding
{
    public string Name { get; }

    public LocalizedText Description { get; }

    public IReadOnlyList<string> Aliases { get; }

    public IReadOnlyList<CommandRoute> Routes { get; }

    public IReadOnlySet<CommandSource> Sources { get; }

    public TextCommand(
        string name,
        LocalizedText description,
        IEnumerable<CommandRoute> routes,
        IEnumerable<string>? aliases = null,
        IEnumerable<CommandSource>? sources = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = ValidateName(name, nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Routes = routes?.ToArray() ?? throw new ArgumentNullException(nameof(routes));
        Aliases = aliases?.Select(alias => ValidateName(alias, nameof(aliases))).ToArray() ?? [];
        Sources = new HashSet<CommandSource>(
            sources ?? Enum.GetValues<CommandSource>()
                .Where(source => source is not CommandSource.Local));
        if (Routes.Count == 0)
        {
            throw new ArgumentException("A command must define at least one route.", nameof(routes));
        }

        foreach (var route in Routes)
        {
            var duplicateArgument = route.Segments
                .OfType<CommandArgument>()
                .GroupBy(argument => argument.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateArgument is not null)
            {
                throw new ArgumentException(
                    $"Command route contains duplicate argument \"{duplicateArgument.Key}\".",
                    nameof(routes));
            }

            foreach (var argument in route.Segments.OfType<CommandArgument>())
            {
                if (argument.Choices?.Any(choice =>
                        string.IsNullOrWhiteSpace(choice) || choice.Any(char.IsWhiteSpace)) == true)
                {
                    throw new ArgumentException(
                        $"Choices for argument \"{argument.Name}\" must be non-empty single tokens.",
                        nameof(routes));
                }
            }
        }

        if (Sources.Count == 0)
        {
            throw new ArgumentException(
                "A text command adapter must support at least one command source.",
                nameof(sources));
        }
    }

    public bool SupportsSource(CommandSource source) => Sources.Contains(source);

    private static string ValidateName(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.StartsWith('/') || value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "Command names and aliases must be single tokens without a leading slash.",
                parameterName);
        }

        return value;
    }
}

public sealed record CommandSuggestion(string Value, string Description, bool IsArgument);

public sealed record RegisteredTextCommand(ResourceId Id, TextCommand Command);
