using EntitySystem.Core;

using Game.Localization;
using Game.Network;
using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Commands;

/// <summary>
/// A concrete operation. Frontends create commands; domains determine routing.
/// </summary>
public interface IGameCommand;

public interface ICommandDefinition
{
    Type CommandType { get; }

    LocalizedText Description { get; }

    CommandDomain Domain { get; }

    ResourceId? RequiredPermission { get; }

    CommandHostRequirement HostRequirement { get; }

    CommandPrincipalKind AllowedPrincipals { get; }

    bool CanInvoke(CommandPrincipal principal, Project? project);

    bool CanExecuteHere(RunModeType runMode, WorkType workType);

    bool IsAuthorized(CommandContext context, IGameCommand command);

    bool IsPotentiallyAuthorized(
        CommandPermissionRegistry permissions,
        CommandPrincipal principal,
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
    private readonly Func<PackageStreamReader, TCommand>? _read;
    private readonly Action<PackageStreamWriter, TCommand>? _write;

    public Type CommandType => typeof(TCommand);

    public LocalizedText Description { get; }

    public CommandDomain Domain { get; }

    public ResourceId? RequiredPermission { get; }

    public CommandHostRequirement HostRequirement { get; }

    public CommandPrincipalKind AllowedPrincipals { get; }

    public bool SupportsRemoteInvocation => _write is not null && _read is not null;

    public CommandDefinition(
        Func<CommandContext, TCommand, CommandResult> handler,
        CommandDomain domain,
        LocalizedText? description = null,
        ResourceId? requiredPermission = null,
        CommandHostRequirement hostRequirement = CommandHostRequirement.None,
        CommandPrincipalKind? allowedPrincipals = null,
        Action<PackageStreamWriter, TCommand>? write = null,
        Func<PackageStreamReader, TCommand>? read = null)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        Domain = domain;
        Description = description ?? LocalizedText.Empty;
        RequiredPermission = requiredPermission;
        HostRequirement = hostRequirement;
        AllowedPrincipals = allowedPrincipals ?? GetDefaultPrincipals(domain);
        if ((write is null) != (read is null))
        {
            throw new ArgumentException(
                "Command definitions must provide both write and read delegates.");
        }

        _write = write;
        _read = read;
    }

    public bool CanInvoke(CommandPrincipal principal, Project? project)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if ((AllowedPrincipals & principal.Kind) == 0)
        {
            return false;
        }

        if (HostRequirement is CommandHostRequirement.HeadlessServer &&
            (CommonLib.WorkType is not WorkType.Server ||
             RunMode.Value is not RunModeType.HeadlessServer))
        {
            return false;
        }
        if (HostRequirement is CommandHostRequirement.Gui &&
            RunMode.Value is not RunModeType.Gui)
        {
            return false;
        }

        return Domain switch
        {
            CommandDomain.Application =>
                principal.Is(CommandPrincipalKind.ApplicationUser) ||
                principal.Is(CommandPrincipalKind.System),
            CommandDomain.World =>
                principal.Is(CommandPrincipalKind.Player) ||
                principal.Is(CommandPrincipalKind.ServerOperator) ||
                principal.Is(CommandPrincipalKind.System),
            CommandDomain.Server =>
                CommonLib.WorkType is WorkType.Client or WorkType.Server &&
                (principal.Is(CommandPrincipalKind.Player) ||
                 principal.Is(CommandPrincipalKind.ServerOperator) ||
                 principal.Is(CommandPrincipalKind.System)),
            _ => false
        };
    }

    public bool CanExecuteHere(RunModeType runMode, WorkType workType)
    {
        if (HostRequirement is CommandHostRequirement.HeadlessServer &&
            (workType is not WorkType.Server ||
             runMode is not RunModeType.HeadlessServer))
        {
            return false;
        }
        if (HostRequirement is CommandHostRequirement.Gui && runMode is not RunModeType.Gui)
        {
            return false;
        }

        return Domain switch
        {
            CommandDomain.Application => true,
            CommandDomain.World => workType is WorkType.Local or WorkType.Server,
            CommandDomain.Server => workType is WorkType.Server,
            _ => false
        };
    }

    public CommandResult Handle(CommandContext context, IGameCommand command)
    {
        return _handler(context, (TCommand)command);
    }

    public bool IsAuthorized(CommandContext context, IGameCommand command)
    {
        return RequiredPermission is not { } permission ||
               context.Registry.Permissions.HasEffectivePermission(
                   permission,
                   context.Principal,
                   context.Project);
    }

    public bool IsPotentiallyAuthorized(
        CommandPermissionRegistry permissions,
        CommandPrincipal principal,
        Project? project)
    {
        return CanInvoke(principal, project) &&
               (RequiredPermission is not { } permission ||
                permissions.HasEffectivePermission(
                    permission,
                    principal,
                    project));
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

    private static CommandPrincipalKind GetDefaultPrincipals(
        CommandDomain domain)
    {
        return domain switch
        {
            CommandDomain.Application =>
                CommandPrincipalKind.ApplicationUser |
                CommandPrincipalKind.System,
            CommandDomain.World or CommandDomain.Server =>
                CommandPrincipalKind.Player |
                CommandPrincipalKind.ServerOperator |
                CommandPrincipalKind.System,
            _ => CommandPrincipalKind.None
        };
    }
}

public sealed record RegisteredGameCommand(
    ResourceId Id,
    ICommandDefinition Definition);
