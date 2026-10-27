using EntitySystem.Core;

using Game.Network;

namespace Game.Commands;

/// <summary>
///     A concrete operation. Frontends create commands; domains determine routing.
/// </summary>
public interface IGameCommand;

public interface ICommandDefinition
{
    Type CommandType { get; }

    LocalizedText Description { get; }

    CommandDomain Domain { get; }

    CommandPanelBehavior PanelBehavior { get; }

    ResourceId? RequiredPermission { get; }

    CommandHostRequirement HostRequirement { get; }

    CommandPrincipalKinds AllowedPrincipals { get; }

    bool CanInvoke(CommandPrincipal principal, Project? project);

    bool CanExecuteHere(RunModeType runMode, WorkType workType);

    bool IsAuthorized(CommandContext context, IGameCommand command);

    bool IsPotentiallyAuthorized(
        CommandPermissionRegistry permissions,
        CommandPrincipal principal,
        Project? project
    );

    CommandResult Handle(CommandContext context, IGameCommand command);

    bool SupportsRemoteInvocation { get; }

    byte[] Encode(IGameCommand command);

    IGameCommand Decode(byte[] payload);
}

public sealed class CommandDefinition<TCommand>(
    Func<CommandContext, TCommand, CommandResult> handler,
    CommandDomain domain,
    LocalizedText? description = null,
    ResourceId? requiredPermission = null,
    CommandHostRequirement hostRequirement = CommandHostRequirement.None,
    CommandPrincipalKinds? allowedPrincipals = null,
    bool supportsRemoteInvocation = false,
    CommandPanelBehavior panelBehavior = CommandPanelBehavior.ReturnToMessage
)
    : ICommandDefinition
    where TCommand : IGameCommand
{
    private readonly Func<CommandContext, TCommand, CommandResult> _handler =
        handler ?? throw new ArgumentNullException(nameof(handler));

    public Type CommandType => typeof(TCommand);

    public LocalizedText Description { get; } = description ?? LocalizedText.Empty;

    public CommandDomain Domain { get; } = domain;

    public CommandPanelBehavior PanelBehavior { get; } = panelBehavior;

    public ResourceId? RequiredPermission { get; } = requiredPermission;

    public CommandHostRequirement HostRequirement { get; } = hostRequirement;

    public CommandPrincipalKinds AllowedPrincipals { get; } = allowedPrincipals ?? GetDefaultPrincipals(domain);

    public bool SupportsRemoteInvocation { get; } = supportsRemoteInvocation;

    public bool CanInvoke(CommandPrincipal principal, Project? project)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (!Allows(principal.Kind))
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

        return true;
    }

    private bool Allows(CommandPrincipalKind kind)
    {
        var flag = kind switch
        {
            CommandPrincipalKind.LocalOperator => CommandPrincipalKinds.LocalOperator,
            CommandPrincipalKind.Player => CommandPrincipalKinds.Player,
            CommandPrincipalKind.System => CommandPrincipalKinds.System,
            _ => CommandPrincipalKinds.None
        };
        return (AllowedPrincipals & flag) != 0;
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
                   context.Project
               );
    }

    public bool IsPotentiallyAuthorized(
        CommandPermissionRegistry permissions,
        CommandPrincipal principal,
        Project? project
    )
    {
        return CanInvoke(principal, project) &&
               (RequiredPermission is not { } permission ||
                permissions.HasEffectivePermission(
                    permission,
                    principal,
                    project
                ));
    }

    public byte[] Encode(IGameCommand command)
    {
        if (!SupportsRemoteInvocation)
        {
            throw new InvalidOperationException($"Command {typeof(TCommand).Name} does not support remote invocation.");
        }

        return ProtocolPayloadCodec.Encode((TCommand)command);
    }

    public IGameCommand Decode(byte[] payload)
    {
        if (!SupportsRemoteInvocation)
        {
            throw new InvalidOperationException($"Command {typeof(TCommand).Name} does not support remote invocation.");
        }

        return ProtocolPayloadCodec.Decode<TCommand>(payload);
    }

    private static CommandPrincipalKinds GetDefaultPrincipals(CommandDomain domain)
    {
        return domain switch
        {
            CommandDomain.Application =>
                CommandPrincipalKinds.LocalOperator |
                CommandPrincipalKinds.System,
            CommandDomain.World or CommandDomain.Server =>
                CommandPrincipalKinds.LocalOperator |
                CommandPrincipalKinds.Player |
                CommandPrincipalKinds.System,
            _ => CommandPrincipalKinds.None
        };
    }
}

public sealed record RegisteredGameCommand(
    ResourceId Id,
    ICommandDefinition Definition
);
