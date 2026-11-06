using EntitySystem.Core;

using Game.Commands.Text;

namespace Game.Commands;

public static class CommandExecutor
{
    public static CommandResult ExecutePlayer(
        CommandRegistry? registry,
        string input,
        CharacterData player,
        string? correlationId = null
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        return Execute(
            registry,
            input,
            CommandInvocationChannel.Text,
            CommandPrincipal.FromPlayer(player),
            player.Project,
            correlationId
        );
    }

    public static CommandResult ExecuteLocal(
        CommandRegistry? registry,
        string input,
        Project? project,
        string? correlationId = null
    )
    {
        return Execute(
            registry,
            input,
            CommandInvocationChannel.Console,
            CommandPrincipal.LocalOperator,
            project,
            correlationId
        );
    }

    public static CommandResult ExecutePlayer(
        CommandRegistry? registry,
        IGameCommand command,
        CharacterData player,
        string? correlationId = null
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        return Execute(
            registry,
            command,
            CommandInvocationChannel.UserInterface,
            CommandPrincipal.FromPlayer(player),
            player.Project,
            correlationId
        );
    }

    public static CommandResult ExecuteLocal(
        CommandRegistry? registry,
        IGameCommand command,
        Project? project,
        string? correlationId = null
    )
    {
        return Execute(
            registry,
            command,
            CommandInvocationChannel.UserInterface,
            CommandPrincipal.LocalOperator,
            project,
            correlationId
        );
    }

    internal static CommandResult ExecuteSystem(CommandRegistry? registry, IGameCommand command, Project? project)
    {
        return Execute(registry, command, CommandInvocationChannel.Internal, CommandPrincipal.System, project, null);
    }

    private static CommandResult Execute(
        CommandRegistry? registry,
        string input,
        CommandInvocationChannel channel,
        CommandPrincipal principal,
        Project? project,
        string? correlationId
    )
    {
        if (registry is null)
        {
            return CommandResult.LocalizedFail(
                "command.unavailable",
                "CommandUnavailable_Message",
                "指令系统尚未就绪。"
            );
        }

        var context = new CommandContext(
            channel,
            principal,
            project,
            correlationId
        );
        return new TextCommandAdapter(registry, project).Execute(input, context);
    }

    private static CommandResult Execute(
        CommandRegistry? registry,
        IGameCommand command,
        CommandInvocationChannel channel,
        CommandPrincipal principal,
        Project? project,
        string? correlationId
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (registry is null)
        {
            return CommandResult.LocalizedFail(
                "command.unavailable",
                "CommandUnavailable_Message",
                "命令系统尚未就绪。"
            );
        }

        var context = new CommandContext(
            channel,
            principal,
            project,
            correlationId
        );
        return new CommandDispatcher(registry).Execute(command, context);
    }
}
