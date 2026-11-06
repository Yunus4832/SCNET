using EntitySystem.Core;

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


    CommandPrincipalKinds AllowedPrincipals { get; }

    bool CanInvoke(CommandPrincipal principal, Project? project);

    bool CanExecuteHere(Project? project);

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


    public CommandPrincipalKinds AllowedPrincipals { get; } = allowedPrincipals ?? GetDefaultPrincipals(domain);

    public bool SupportsRemoteInvocation { get; } = supportsRemoteInvocation;

    public bool CanInvoke(CommandPrincipal principal, Project? project)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (!Allows(principal.Kind))
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

    private static bool HasAuthority(Project? project)
    {
        return project is { IsDisposed: false } &&
               WorldRuntimeContext.For(project) is { IsClosed: false, IsAuthority: true };
    }

    public bool CanExecuteHere(Project? project)
    {
        return Domain switch
        {
            CommandDomain.Application => true,
            CommandDomain.World => HasAuthority(project),
            CommandDomain.Server => HasAuthority(project),
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
