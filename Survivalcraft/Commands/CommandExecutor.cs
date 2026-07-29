using EntitySystem.Core;

using Game.Modding;

namespace Game.Commands;

public static class CommandExecutor
{
    public static CommandResult ExecuteLocal(
        string input,
        Project? project,
        string? correlationId = null)
    {
        return Execute(
            input,
            CommandSource.Local,
            CommandPrincipal.Local,
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
            CommandSource.Player,
            CommandPrincipal.FromPlayer(player),
            player.Project,
            correlationId);
    }

    public static CommandResult ExecuteServerConsole(
        string input,
        Project? project,
        string? correlationId = null)
    {
        return Execute(
            input,
            CommandSource.ServerConsole,
            CommandPrincipal.ServerConsole,
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
            CommandSource.Player,
            CommandPrincipal.FromPlayer(player),
            player.Project,
            correlationId);
    }

    public static CommandResult ExecuteLocal(
        IGameCommand command,
        Project? project,
        string? correlationId = null)
    {
        return Execute(
            command,
            CommandSource.Local,
            CommandPrincipal.Local,
            project,
            correlationId);
    }

    public static CommandResult ExecuteLocalHost(
        IGameCommand command,
        Project? project,
        string? correlationId = null)
    {
        return Execute(
            command,
            CommandSource.Local,
            CommandPrincipal.LocalHost,
            project,
            correlationId);
    }

    public static CommandResult ExecuteServerConsole(
        IGameCommand command,
        Project? project,
        string? correlationId = null)
    {
        return Execute(
            command,
            CommandSource.ServerConsole,
            CommandPrincipal.ServerConsole,
            project,
            correlationId);
    }

    private static CommandResult Execute(
        string input,
        CommandSource source,
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
            source,
            principal,
            project,
            correlationId);
        return new TextCommandAdapter(runtime.Commands).Execute(input, context);
    }

    private static CommandResult Execute(
        IGameCommand command,
        CommandSource source,
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
            source,
            principal,
            project,
            correlationId);
        return new CommandDispatcher(runtime.Commands).Execute(command, context);
    }
}
