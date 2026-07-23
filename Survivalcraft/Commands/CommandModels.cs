using EntitySystem.Core;

using Game.Network;
using Game.Network.Enums;

namespace Game.Commands;

public enum CommandSource
{
    Player,
    ServerConsole,
    Mod,
    DebugApi
}

public enum CommandArgumentKind
{
    String,
    Integer,
    Number,
    Boolean
}

public enum CommandExecutionEnvironment
{
    Any,
    Server,
    HeadlessServer
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
            return _delegablePermissions.Count > 0;
        }

        if (_permissions.Any(granted => CommandPermissionSet.Implies(granted, normalized)))
        {
            return true;
        }

        return false;
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
        if (HasGuiServerOwnerBootstrapAuthority(
                RunMode.Value,
                CommonLib.WorkType,
                player.ServerMaster))
        {
            return new CommandPrincipal(
                player.Name,
                player,
                permissions: ["*"],
                delegablePermissions: ["*"]);
        }

        return new CommandPrincipal(
            player.Name,
            player,
            player.CommandPermissions.Grants.Select(grant => grant.Permission),
            player.CommandPermissions.Grants
                .Where(grant => grant.CanDelegate)
                .Select(grant => grant.Permission));
    }

    internal static bool HasGuiServerOwnerBootstrapAuthority(
        RunModeType runMode,
        WorkType workType,
        bool isServerMaster)
    {
        return runMode is RunModeType.Gui &&
               workType is WorkType.Server &&
               isServerMaster;
    }

    public static CommandPrincipal ServerConsole { get; } =
        new("Server", permissions: ["*"], delegablePermissions: ["*"]);
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

public sealed record CommandResult(bool Success, string Code, string Message)
{
    public static CommandResult Ok(string message, string code = "command.ok") => new(true, code, message);

    public static CommandResult Fail(string code, string message) => new(false, code, message);
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

public sealed record CommandArgumentSuggestion(string Value, string Description = "");

public sealed class CommandSuggestionContext(
    CommandRegistry registry,
    CommandPrincipal principal,
    IReadOnlyList<string> completedTokens)
{
    public CommandRegistry Registry { get; } = registry;

    public CommandPrincipal Principal { get; } = principal;

    public IReadOnlyList<string> CompletedTokens { get; } = completedTokens;

    public Project? Project => Principal.Player?.Project ?? GameManager.Project;
}

public sealed class CommandRoute(
    IEnumerable<CommandSegment> segments,
    Func<CommandContext, CommandArguments, CommandResult> execute,
    string description = "",
    string requiredPermission = ""
)
{
    public IReadOnlyList<CommandSegment> Segments { get; } =
        segments?.ToArray() ?? throw new ArgumentNullException(nameof(segments));

    public string Description { get; } = description;

    public string RequiredPermission { get; } = requiredPermission;

    public Func<CommandContext, CommandArguments, CommandResult> Execute { get; } =
        execute ?? throw new ArgumentNullException(nameof(execute));
}

public sealed class GameCommand
{
    public string Name { get; }

    public string Description { get; }

    public IReadOnlyList<string> Aliases { get; }

    public IReadOnlyList<CommandRoute> Routes { get; }

    /// <summary>
    /// Used to filter discovery UI. Command handlers must still validate their runtime requirements.
    /// </summary>
    public CommandExecutionEnvironment ExecutionEnvironment { get; }

    public GameCommand(
        string name,
        string description,
        IEnumerable<CommandRoute> routes,
        IEnumerable<string>? aliases = null,
        CommandExecutionEnvironment executionEnvironment = CommandExecutionEnvironment.Any)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = ValidateName(name, nameof(name));
        Description = description;
        Routes = routes?.ToArray() ?? throw new ArgumentNullException(nameof(routes));
        Aliases = aliases?.Select(alias => ValidateName(alias, nameof(aliases))).ToArray() ?? [];
        ExecutionEnvironment = executionEnvironment;
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
    }

    public bool IsAvailable(RunModeType runMode, WorkType workType)
    {
        return ExecutionEnvironment switch
        {
            CommandExecutionEnvironment.Any => true,
            CommandExecutionEnvironment.Server => workType is WorkType.Server,
            CommandExecutionEnvironment.HeadlessServer =>
                workType is WorkType.Server && runMode is RunModeType.HeadlessServer,
            _ => false
        };
    }

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

public sealed record RegisteredCommand(ResourceId Id, GameCommand Command);
