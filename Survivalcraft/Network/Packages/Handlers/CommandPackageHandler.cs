using EntitySystem.Core;

using Game.Commands;
using Game.Messaging;

namespace Game.Network.Packages.Handlers;

public sealed class CommandPackageHandler : PackageHandlerBase<CommandPackage>
{
    private const int _maximumCommandLength = 512;

    private static readonly long _minimumCommandInterval = Math.Max(1, Stopwatch.Frequency / 10);

    private readonly Dictionary<Guid, long> _lastCommandTimestamp = [];

    public override void Handle(CommandPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode is null || GameManager.Project is not { } project)
        {
            return;
        }

        if (isServer)
        {
            HandleServer(package, project);
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
                    .Messages.DisplayLocal(GameMessage.Command("你的指令权限已更新。", success: true));
            }

            return;
        }
    }

    private void HandleServer(CommandPackage package, Project project)
    {
        if (package.Mode is not CommandPackage.CommandPackageMode.Request ||
            package.From is null)
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

        if (_lastCommandTimestamp.TryGetValue(package.From.GUID, out var last) &&
            now - last < _minimumCommandInterval)
        {
            result = CommandResult.Fail("command.rate_limited", "指令发送过于频繁。");
        }
        else
        {
            _lastCommandTimestamp[package.From.GUID] = now;
            if (package.Input.Length > _maximumCommandLength)
            {
                result = CommandResult.Fail("command.too_long", "指令长度超过限制。");
            }
            else
            {
                var player = package.From.PlayerData;
                result = CommandExecutor.ExecutePlayer(
                    package.Input,
                    player,
                    package.CorrelationId);
                Log.Information(
                    $"Command executed: principal={player.Name}, code={result.Code}, " +
                    $"success={result.Success}, correlation={package.CorrelationId}");
            }
        }

        CommandResultPublisher.Publish(project, result, package.From.ID);
    }
}
