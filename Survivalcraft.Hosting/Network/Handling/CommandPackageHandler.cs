using EntitySystem.Core;

using Game.Commands.Text;

namespace Game.Network.Handling;

public class CommandPackageHandler(CommandRegistry commands) : PackageHandlerBase<CommandPackage>
{
    private readonly CommandRegistry _commands = commands ?? throw new ArgumentNullException(nameof(commands));

    private const int _maximumCommandLength = 512;

    private static readonly long _minimumCommandInterval = Math.Max(1, Stopwatch.Frequency / 10);

    private readonly Dictionary<PlayerAddress, long> _lastCommandTimestamp = [];

    private readonly Dictionary<PlayerAddress, long> _lastSuggestionTimestamp = [];

    protected static bool IsAuthorityReply(PackageReceiveContext context) =>
        !context.IsServer &&
        !context.Runtime.IsClosed &&
        context.Node?.Server is { } authority &&
        ReferenceEquals(context.Sender, authority) &&
        ReferenceEquals(authority.World, context.Runtime);

    public override void Handle(CommandPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (netNode is null || context.Project is not { } project)
        {
            return;
        }

        if (isServer)
        {
            if (package is CommandSuggestionsRequestPackage completionRequest)
            {
                if (context.Sender is not { } sender ||
                    WorldConnectionResolver.FindPlayer(sender) is null ||
                    sender.PlayerAddress is null ||
                    completionRequest.Input.Length > _maximumCommandLength)
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
                var suggestions = new TextCommandAdapter(_commands, project).Suggest(
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

        if (!IsAuthorityReply(context)) { return; }

        if (package is CommandPermissionSnapshotPackage permissions)
        {
            if (permissions.PlayerAddress is not { } address || netNode.Self?.PlayerAddress != address)
            {
                return;
            }

            var players = context.Project.FindSubsystem<SubsystemPlayers>(true)!;
            players.Store.GetOrCreate(address).Permissions.Replace(permissions.PermissionGrants);
        }
    }

    private void HandleServer(CommandRequestPackage package, Project project, PackageReceiveContext context)
    {
        if (context.Sender is not { } sender ||
            WorldConnectionResolver.FindPlayer(sender) is null ||
            sender.PlayerAddress is null)
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

        if (_lastCommandTimestamp.TryGetValue(sender.PlayerAddress!.Value, out var last) &&
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
            _lastCommandTimestamp[sender.PlayerAddress!.Value] = now;
            result = ExecuteRequest(package, context.Sender, project);
        }

        CommandResultPublisher.PublishRemote(
            project,
            result,
            context.Sender,
            package.CorrelationId
        );
    }

    private CommandResult ExecuteRequest(
        CommandRequestPackage package,
        WorldConnection session,
        Project project
    )
    {
        if (!_commands.TryDecode(
                package.CommandId,
                package.Payload,
                out var command,
                out var error
            ))
        {
            return error! with { Code = "command.invalid_payload" };
        }

        return new CommandDispatcher(_commands).Execute(
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
