using EntitySystem.Core;

using Game.Commands;
using Game.Messaging;

namespace Game.Network.Packages.Handlers;

public sealed class CommandPackageHandler : PackageHandlerBase<CommandPackage>
{
    private const int _maximumCommandLength = 512;

    private static readonly long _minimumCommandInterval = Math.Max(1, Stopwatch.Frequency / 10);

    private readonly Dictionary<Guid, long> _lastCommandTimestamp = [];

    private readonly Dictionary<Guid, long> _lastSuggestionTimestamp = [];

    public override void Handle(CommandPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (netNode is null || GameManager.Project is not { } project)
        {
            return;
        }

        if (isServer)
        {
            if (package.Mode is CommandPackage.CommandPackageMode.SuggestionRequest)
            {
                if (context.Sender is not { } sender || package.Input.Length > _maximumCommandLength ||
                    CurrentModRuntime.Value is not { } runtime)
                {
                    return;
                }

                var now = Stopwatch.GetTimestamp();
                if (_lastSuggestionTimestamp.TryGetValue(sender.GUID, out var last) && now - last < _minimumCommandInterval)
                {
                    return;
                }

                if (_lastSuggestionTimestamp.Count > 1024)
                {
                    _lastSuggestionTimestamp.Clear();
                }

                _lastSuggestionTimestamp[sender.GUID] = now;
                var suggestions = new TextCommandAdapter(runtime.Commands).Suggest(package.Input, CommandPrincipal.FromPlayer(sender.PlayerData));
                netNode.QueuePackage(CommandPackage.CreateSuggestions(package.Input, package.CorrelationId, suggestions), PackageAudience.To(sender));
                return;
            }

            HandleServer(package, project, context);
            return;
        }

        if (package.Mode is CommandPackage.CommandPackageMode.Suggestions)
        {
            CommonLib.MainPlayer?.GameWidget.MessagePanel?.ReceiveSuggestions(package.Input, package.CorrelationId, package.Suggestions);
            return;
        }

        if (package.Mode is CommandPackage.CommandPackageMode.Result &&
            package.Result is { } result)
        {
            DialogsManager.HideLoadingDialogs();
            CommandResultPublisher.DisplayLocal(project, result, package.CorrelationId);
            return;
        }

        if (package.Mode is CommandPackage.CommandPackageMode.PermissionSnapshot)
        {
            var players = GameManager.Project.FindSubsystem<SubsystemPlayers>(true)!;
            players.FindPlayerData(player => player.PlayerGUID == package.PlayerGuid)?
                .CommandPermissions.Replace(package.PermissionGrants);
            if (CommonLib.MainPlayer?.PlayerData.PlayerGUID == package.PlayerGuid)
            {
                GameManager.Project.FindSubsystem<SubsystemGameWidgets>(true)!
                    .Messages.DisplayLocal(GameMessage.Command(
                        CommandText.Get(
                            "PermissionUpdated_Message",
                            "你的指令权限已更新。"),
                        success: true));
            }
        }
    }

    private void HandleServer(CommandPackage package, Project project, PackageReceiveContext context)
    {
        if (package.Mode is not (
                CommandPackage.CommandPackageMode.Request or
                CommandPackage.CommandPackageMode.TypedRequest) ||
            context.Sender is null)
        {
            return;
        }

        CommandResult result;
        var now = Stopwatch.GetTimestamp();
        if (_lastCommandTimestamp.Count > 1024)
        {
            var cutoff = now - Stopwatch.Frequency * 60;
            foreach (var guid in _lastCommandTimestamp
                         .Where(pair => pair.Value < cutoff)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                _lastCommandTimestamp.Remove(guid);
            }
        }

        if (_lastCommandTimestamp.TryGetValue(context.Sender.GUID, out var last) &&
            now - last < _minimumCommandInterval)
        {
            result = CommandResult.LocalizedFail(
                "command.rate_limited",
                "CommandRateLimited_Message",
                "指令发送过于频繁。");
        }
        else
        {
            _lastCommandTimestamp[context.Sender.GUID] = now;
            if (package.Mode is CommandPackage.CommandPackageMode.Request &&
                package.Input.Length > _maximumCommandLength)
            {
                result = CommandResult.LocalizedFail(
                    "command.too_long",
                    "CommandTooLong_Message",
                    "指令长度超过限制。");
            }
            else if (package.Mode is CommandPackage.CommandPackageMode.TypedRequest)
            {
                result = ExecuteTyped(package, context.Sender.PlayerData);
            }
            else
            {
                var player = context.Sender.PlayerData;
                result = CommandExecutor.ExecutePlayer(
                    package.Input,
                    player,
                    package.CorrelationId);
                Log.Information(
                    $"Command executed: principal={player.Name}, code={result.Code}, " +
                    $"success={result.Success}, correlation={package.CorrelationId}");
            }
        }

        CommandResultPublisher.PublishRemote(
            project,
            result,
            context.Sender,
            package.CorrelationId);
    }

    private static CommandResult ExecuteTyped(
        CommandPackage package,
        PlayerData player)
    {
        if (CurrentModRuntime.Value is not { } runtime)
        {
            return CommandResult.LocalizedFail(
                "command.unavailable",
                "CommandUnavailable_Message",
                "命令系统尚未就绪。");
        }

        if (!runtime.Commands.TryDecode(
                package.CommandId,
                package.Payload,
                out var command,
                out var error))
        {
            return CommandResult.Fail("command.invalid_payload", error);
        }

        return CommandExecutor.ExecutePlayer(
            command!,
            player,
            package.CorrelationId);
    }
}
