using EntitySystem.Core;

namespace Game.Commands;

public static class CommandExecutor
{
    public static CommandResult ExecutePlayer(
        string input,
        CharacterData player,
        string? correlationId = null
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        return Execute(
            input,
            CommandInvocationChannel.Text,
            CommandPrincipal.FromPlayer(player),
            player.Project,
            correlationId
        );
    }

    public static CommandResult ExecuteLocal(
        string input,
        Project? project,
        string? correlationId = null
    )
    {
        return Execute(
            input,
            CommandInvocationChannel.Console,
            CommandPrincipal.LocalOperator,
            project,
            correlationId
        );
    }

    public static CommandResult ExecutePlayer(
        IGameCommand command,
        CharacterData player,
        string? correlationId = null
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        return Execute(
            command,
            CommandInvocationChannel.UserInterface,
            CommandPrincipal.FromPlayer(player),
            player.Project,
            correlationId
        );
    }

    public static CommandResult ExecuteLocal(
        IGameCommand command,
        Project? project,
        string? correlationId = null
    )
    {
        return Execute(
            command,
            CommandInvocationChannel.UserInterface,
            CommandPrincipal.LocalOperator,
            project,
            correlationId
        );
    }

    internal static CommandResult ExecuteSystem(IGameCommand command, Project? project)
    {
        return Execute(command, CommandInvocationChannel.Internal, CommandPrincipal.System, project, null);
    }

    private static CommandResult Execute(
        string input,
        CommandInvocationChannel channel,
        CommandPrincipal principal,
        Project? project,
        string? correlationId
    )
    {
        if (CurrentModRuntime.Value is not { } runtime)
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
        return new TextCommandAdapter(runtime.Commands).Execute(input, context);
    }

    private static CommandResult Execute(
        IGameCommand command,
        CommandInvocationChannel channel,
        CommandPrincipal principal,
        Project? project,
        string? correlationId
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (CurrentModRuntime.Value is not { } runtime)
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
        return new CommandDispatcher(runtime.Commands).Execute(command, context);
    }
}
