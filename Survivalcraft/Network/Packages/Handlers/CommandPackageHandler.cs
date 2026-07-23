using Game.Commands;

namespace Game.Network.Packages.Handlers;

public sealed class CommandPackageHandler : PackageHandlerBase<CommandPackage>
{
    private const int _maximumCommandLength = 512;

    private static readonly long _minimumCommandInterval = Math.Max(1, Stopwatch.Frequency / 10);

    private readonly Dictionary<Guid, long> _lastCommandTimestamp = [];

    public override void Handle(CommandPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode is null || GameManager.Project is null)
        {
            return;
        }

        if (isServer)
        {
            HandleServer(package, netNode);
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
                    .AddNetMessage("<c=green>[指令]</c>你的指令权限已更新。", external: false);
            }

            return;
        }

        if (package.Mode is not CommandPackage.CommandPackageMode.Result)
        {
            return;
        }

        var prefix = package.Success ? "<c=green>[指令]</c>" : "<c=red>[指令]</c>";
        GameManager.Project.FindSubsystem<SubsystemGameWidgets>(true)!
            .AddNetMessage(prefix + package.Message, external: false);
    }

    private void HandleServer(CommandPackage package, NetNode netNode)
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

        var response = CommandPackage.CreateResult(
            package.CorrelationId,
            result.Success,
            result.Code,
            result.Message);
        response.To = package.From;
        netNode.QueuePackage(response);
    }
}
