using EntitySystem.Core;

namespace Game.Commands;

public static class CommandExecutor
{
    public static CommandResult ExecuteApplication(
        string input,
        Project? project,
        string? correlationId = null)
    {
        return Execute(
            input,
            CommandInvocationChannel.Text,
            CommandPrincipal.ApplicationUser,
            project,
            correlationId);
    }

    public static CommandResult ExecutePlayer(
        string input,
        PlayerData player,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        return Execute(
            input,
            CommandInvocationChannel.Text,
            CommandPrincipal.FromPlayer(player),
            player.Project,
            correlationId);
    }

    public static CommandResult ExecuteServerOperator(
        string input,
        Project? project,
        string? correlationId = null)
    {
        return Execute(
            input,
            CommandInvocationChannel.ServerControl,
            CommandPrincipal.ServerOperator,
            project,
            correlationId);
    }

    public static CommandResult ExecutePlayer(
        IGameCommand command,
        PlayerData player,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        return Execute(
            command,
            CommandInvocationChannel.UserInterface,
            CommandPrincipal.FromPlayer(player),
            player.Project,
            correlationId);
    }

    public static CommandResult ExecuteApplication(
        IGameCommand command,
        Project? project,
        string? correlationId = null)
    {
        return Execute(
            command,
            CommandInvocationChannel.UserInterface,
            CommandPrincipal.ApplicationUser,
            project,
            correlationId);
    }

    public static CommandResult ExecuteServerOperator(
        IGameCommand command,
        Project? project,
        string? correlationId = null)
    {
        return Execute(
            command,
            CommandInvocationChannel.ServerControl,
            CommandPrincipal.ServerOperator,
            project,
            correlationId);
    }

    private static CommandResult Execute(
        string input,
        CommandInvocationChannel channel,
        CommandPrincipal principal,
        Project? project,
        string? correlationId)
    {
        if (CurrentModRuntime.Value is not { } runtime)
        {
            return CommandResult.LocalizedFail(
                "command.unavailable",
                "CommandUnavailable_Message",
                "指令系统尚未就绪。");
        }

        var context = new CommandContext(
            channel,
            principal,
            project,
            correlationId);
        return new TextCommandAdapter(runtime.Commands).Execute(input, context);
    }

    private static CommandResult Execute(
        IGameCommand command,
        CommandInvocationChannel channel,
        CommandPrincipal principal,
        Project? project,
        string? correlationId)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (CurrentModRuntime.Value is not { } runtime)
        {
            return CommandResult.LocalizedFail(
                "command.unavailable",
                "CommandUnavailable_Message",
                "命令系统尚未就绪。");
        }

        var context = new CommandContext(
            channel,
            principal,
            project,
            correlationId);
        return new CommandDispatcher(runtime.Commands).Execute(command, context);
    }
}
