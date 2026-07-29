using EntitySystem.Core;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Serialization;
using Game.Localization;

namespace Game.Commands;

/// <summary>
/// A concrete, typed request to perform a game or server operation.
/// Text, UI, stdin and remote APIs are adapters that create these commands.
/// </summary>
public interface IGameCommand;

public interface ICommandDefinition
{
    Type CommandType { get; }

    LocalizedText Description { get; }

    string RequiredPermission { get; }

    CommandSourcePolicy SourcePolicy { get; }

    CommandGrantPolicy GrantPolicy { get; }

    CommandExecutionEnvironment ExecutionEnvironment { get; }

    bool IsSourceAllowed(CommandSource source);

    bool IsAvailable(RunModeType runMode, WorkType workType);

    bool IsAuthorized(CommandContext context, IGameCommand command);

    bool IsPotentiallyAuthorized(
        CommandPrincipal principal,
        CommandSource source,
        Project? project);

    CommandResult Handle(CommandContext context, IGameCommand command);

    bool SupportsRemoteInvocation { get; }

    byte[] Encode(IGameCommand command);

    IGameCommand Decode(byte[] payload);
}

public sealed class CommandDefinition<TCommand> : ICommandDefinition
    where TCommand : IGameCommand
{
    private readonly Func<CommandContext, TCommand, CommandResult> _handler;
    private readonly Func<CommandPrincipal, Project?, bool>? _alternativeAuthorization;
    private readonly Func<PackageStreamReader, TCommand>? _read;
    private readonly Action<PackageStreamWriter, TCommand>? _write;

    public Type CommandType => typeof(TCommand);

    public LocalizedText Description { get; }

    public string RequiredPermission { get; }

    public CommandSourcePolicy SourcePolicy { get; }

    public CommandGrantPolicy GrantPolicy { get; }

    public CommandExecutionEnvironment ExecutionEnvironment { get; }

    public bool SupportsRemoteInvocation => _write is not null && _read is not null;

    public CommandDefinition(
        Func<CommandContext, TCommand, CommandResult> handler,
        LocalizedText? description = null,
        string requiredPermission = "",
        CommandSourcePolicy sourcePolicy = CommandSourcePolicy.Any,
        CommandGrantPolicy? grantPolicy = null,
        CommandExecutionEnvironment executionEnvironment = CommandExecutionEnvironment.Any,
        Action<PackageStreamWriter, TCommand>? write = null,
        Func<PackageStreamReader, TCommand>? read = null,
        Func<CommandPrincipal, Project?, bool>? alternativeAuthorization = null)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        Description = description ?? LocalizedText.Empty;
        RequiredPermission = requiredPermission;
        SourcePolicy = sourcePolicy;
        GrantPolicy = grantPolicy ?? GetDefaultGrantPolicy(requiredPermission);
        ExecutionEnvironment = executionEnvironment;
        if ((write is null) != (read is null))
        {
            throw new ArgumentException(
                "Command definitions must provide both write and read delegates.");
        }

        _write = write;
        _read = read;
        _alternativeAuthorization = alternativeAuthorization;
    }

    public bool IsSourceAllowed(CommandSource source)
    {
        return SourcePolicy switch
        {
            CommandSourcePolicy.Any => true,
            CommandSourcePolicy.LocalOnly => source is CommandSource.Local,
            CommandSourcePolicy.PlayerOnly => source is CommandSource.Player,
            CommandSourcePolicy.ServerConsoleOnly => source is CommandSource.ServerConsole,
            CommandSourcePolicy.HttpApiOnly => source is CommandSource.HttpApi,
            _ => false
        };
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

    public CommandResult Handle(CommandContext context, IGameCommand command)
    {
        return _handler(context, (TCommand)command);
    }

    public bool IsAuthorized(CommandContext context, IGameCommand command)
    {
        return context.Principal.HasPermission(RequiredPermission) ||
               _alternativeAuthorization?.Invoke(
                   context.Principal,
                   context.Project) == true;
    }

    public bool IsPotentiallyAuthorized(
        CommandPrincipal principal,
        CommandSource source,
        Project? project)
    {
        return IsSourceAllowed(source) &&
               (principal.HasPermission(RequiredPermission) ||
                _alternativeAuthorization?.Invoke(principal, project) == true);
    }

    public byte[] Encode(IGameCommand command)
    {
        if (_write is null)
        {
            throw new InvalidOperationException(
                $"Command {typeof(TCommand).Name} does not support remote invocation.");
        }

        using var writer = new PackageStreamWriter();
        _write(writer, (TCommand)command);
        return writer.Data(CommonLib.CompressionPolicy.None);
    }

    public IGameCommand Decode(byte[] payload)
    {
        if (_read is null)
        {
            throw new InvalidOperationException(
                $"Command {typeof(TCommand).Name} does not support remote invocation.");
        }

        using var reader = new PackageStreamReader(payload);
        var command = _read(reader);
        if (reader.BaseStream.Position != reader.BaseStream.Length)
        {
            throw new InvalidDataException(
                $"Command {typeof(TCommand).Name} payload contains trailing data.");
        }

        return command;
    }

    private static CommandGrantPolicy GetDefaultGrantPolicy(string permission)
    {
        return permission.StartsWith("server.", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(permission, "server.*", StringComparison.OrdinalIgnoreCase)
            ? CommandGrantPolicy.Protected
            : CommandGrantPolicy.Standard;
    }
}

public sealed record RegisteredGameCommand(ResourceId Id, ICommandDefinition Definition);
