using Game.Network;

namespace Game.Commands;

public sealed class CommandDispatcher(CommandRegistry registry)
{
    private readonly CommandRegistry _registry =
        registry ?? throw new ArgumentNullException(nameof(registry));

    public CommandResult Execute(IGameCommand command, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        if (!_registry.TryGetDefinition(command.GetType(), out var registered) ||
            registered is null)
        {
            return CommandResult.LocalizedFail(
                "command.unregistered",
                "CommandTypeUnregistered_Message",
                "未注册的命令类型：{0}。",
                command.GetType().Name);
        }

        var definition = registered.Definition;
        if (!definition.IsAvailable(RunMode.Value, CommonLib.WorkType))
        {
            return CommandResult.LocalizedFail(
                "command.unavailable",
                "CommandEnvironmentUnavailable_Message",
                "当前运行环境不支持该命令。");
        }

        if (!definition.IsSourceAllowed(context.Source) ||
            !definition.IsAuthorized(context, command))
        {
            return CommandResult.LocalizedFail(
                "command.forbidden",
                "CommandTypedForbidden_Message",
                "你没有执行该命令的权限。");
        }

        try
        {
            context.Registry = _registry;
            return definition.Handle(context, command);
        }
        catch (Exception exception)
        {
            Log.Error(
                $"Command {registered.Id} failed, principal={context.Principal.Name}, " +
                $"source={context.Source}, correlation={context.CorrelationId}: {exception}");
            return CommandResult.LocalizedFail(
                "command.failed",
                "CommandFailed_Message",
                "命令执行失败，详细信息已写入服务器日志。");
        }
    }
}
