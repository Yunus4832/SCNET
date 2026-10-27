using EntitySystem.Core;

using Game.Commands;
using Game.Localization;
using Game.Network.Transport;
using Game.Protocol.Identity;

namespace Game.Network.Handling;

public sealed class CommandPackageHandler : PackageHandlerBase<CommandPackage>
{
    private const int _maximumCommandLength = 512;

    private static readonly long _minimumCommandInterval = Math.Max(1, Stopwatch.Frequency / 10);

    private readonly Dictionary<PlayerAddress, long> _lastCommandTimestamp = [];

    private readonly Dictionary<PlayerAddress, long> _lastSuggestionTimestamp = [];

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
            if (package is CommandSuggestionsRequestPackage completionRequest)
            {
                if (context.Sender is not { } sender ||
                    sender.Subject is null ||
                    sender.PlayerAddress is null ||
                    completionRequest.Input.Length > _maximumCommandLength ||
                    CurrentModRuntime.Value is not { } runtime)
                {
                    return;
                }

                var now = Stopwatch.GetTimestamp();
                if (_lastSuggestionTimestamp.TryGetValue(sender.PlayerAddress!.Value, out var last) &&
                    now - last < _minimumCommandInterval)
                {
                    return;
                }

                if (_lastSuggestionTimestamp.Count > 1024)
                {
                    _lastSuggestionTimestamp.Clear();
                }

                _lastSuggestionTimestamp[sender.PlayerAddress!.Value] = now;
                var suggestions = new TextCommandAdapter(runtime.Commands).Suggest(
                    completionRequest.Input,
                    CommandPrincipal.FromSession(sender)
                );
                netNode.QueuePackage(
                    CommandPackage.CreateSuggestions(
                        completionRequest.Input,
                        completionRequest.CorrelationId,
                        suggestions
                    ),
                    PackageAudience.To(sender)
                );
                return;
            }

            if (package is CommandRequestPackage commandRequest) { HandleServer(commandRequest, project, context); }

            return;
        }

        if (package is CommandSuggestionsPackage completion)
        {
            CommonLib.LocalCharacter?.GameWidget.MessagePanel?.ReceiveSuggestions(
                completion.Input,
                completion.CorrelationId,
                completion.Suggestions.Select(value => value with
                    {
                        Description = value.DescriptionSource?.Resolve() ?? value.Description
                    }
                ).ToArray()
            );
            return;
        }

        if (package is CommandResultPackage { Result: { } result })
        {
            if (!ReferenceEquals(context.Sender, netNode.Server))
            {
                return;
            }

            var restored = CommandResponseBuilder.Restore(result);
            if (!HttpPlayerCommandAdapter.TryReceive(package.CorrelationId, restored))
            {
                DialogsManager.HideLoadingDialogs();
                CommandResultPublisher.DisplayLocal(project, restored, package.CorrelationId);
            }

            return;
        }

        if (package is CommandPermissionSnapshotPackage permissions)
        {
            if (permissions.PlayerAddress is not { } address || CommonLib.Net.Self?.PlayerAddress != address)
            {
                return;
            }

            var players = GameManager.Project.FindSubsystem<SubsystemPlayers>(true)!;
            players.Store.GetOrCreate(address).Permissions.Replace(permissions.PermissionGrants);
            GameManager.Project.FindSubsystem<SubsystemGameWidgets>(true)!.Messages.DisplayLocal(
                GameMessage.Command(CommandText.Get("PermissionUpdated_Message", "你的指令权限已更新。"), success: true)
            );
        }
    }

    private void HandleServer(CommandRequestPackage package, Project project, PackageReceiveContext context)
    {
        if (context.Sender?.Subject is null ||
            context.Sender.PlayerAddress is null)
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

        if (_lastCommandTimestamp.TryGetValue(context.Sender.PlayerAddress!.Value, out var last) &&
            now - last < _minimumCommandInterval)
        {
            result = CommandResult.LocalizedFail(
                "command.rate_limited",
                "CommandRateLimited_Message",
                "指令发送过于频繁。"
            );
        }
        else
        {
            _lastCommandTimestamp[context.Sender.PlayerAddress!.Value] = now;
            result = ExecuteRequest(package, context.Sender, project);
        }

        CommandResultPublisher.PublishRemote(
            project,
            result,
            context.Sender,
            package.CorrelationId
        );
    }

    private static CommandResult ExecuteRequest(
        CommandRequestPackage package,
        Client session,
        Project project
    )
    {
        if (CurrentModRuntime.Value is not { } runtime)
        {
            return CommandResult.LocalizedFail(
                "command.unavailable",
                "CommandUnavailable_Message",
                "命令系统尚未就绪。"
            );
        }

        if (!runtime.Commands.TryDecode(
                package.CommandId,
                package.Payload,
                out var command,
                out var error
            ))
        {
            return CommandResult.Fail("command.invalid_payload", error);
        }

        return new CommandDispatcher(runtime.Commands).Execute(
            command!,
            new CommandContext(
                CommandInvocationChannel.Network,
                CommandPrincipal.FromSession(session),
                project,
                package.CorrelationId
            )
        );
    }
}
