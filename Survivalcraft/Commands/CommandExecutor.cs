using EntitySystem.Core;

using Game.Modding;

namespace Game.Commands;

public static class CommandExecutor
{
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

    private static CommandResult Execute(
        string input,
        CommandSource source,
        CommandPrincipal principal,
        Project? project,
        string? correlationId)
    {
        if (CurrentModRuntime.Value is not { } runtime)
        {
            return CommandResult.Fail("command.unavailable", "指令系统尚未就绪。");
        }

        var context = new CommandContext(
            source,
            principal,
            project,
            correlationId);
        return new CommandDispatcher(runtime.Commands).Execute(input, context);
    }
}
