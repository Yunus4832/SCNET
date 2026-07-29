using EntitySystem.Core;

using Game.Modding;
using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Commands;

/// <summary>
/// Routes a player command to the local authoritative server or to the remote
/// server without exposing transport details to UI adapters.
/// </summary>
public static class CommandGateway
{
    public static string SubmitServer(
        Project project,
        IGameCommand command,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(command);
        var requestId = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId;
        var result = CommandExecutor.ExecuteServerConsole(
            command,
            project,
            requestId);
        CommandResultPublisher.Publish(project, result);
        return requestId;
    }

    public static string Submit(
        PlayerData player,
        string input,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        var requestId = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId;
        if (CurrentModRuntime.Value is { } runtime)
        {
            var adapter = new TextCommandAdapter(runtime.Commands);
            if (adapter.SupportsSource(input, CommandSource.Local))
            {
                var result = CommandExecutor.ExecuteLocal(
                    input,
                    player.Project,
                    requestId);
                CommandResultPublisher.DisplayLocal(player.Project, result);
                return requestId;
            }
        }

        if (CommonLib.WorkType is WorkType.Local or WorkType.Server)
        {
            var result = CommandExecutor.ExecutePlayer(input, player, requestId);
            CommandResultPublisher.Publish(
                player.Project,
                result,
                player.Client?.ID,
                includeServer: player.IsMainPlayer);
            return requestId;
        }

        CommonLib.Net.QueuePackage(CommandPackage.CreateRequest(input, requestId));
        return requestId;
    }

    public static string Submit(
        PlayerData player,
        IGameCommand command,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(command);
        var requestId = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId;
        if (CommonLib.WorkType is WorkType.Local or WorkType.Server)
        {
            var result = CommandExecutor.ExecutePlayer(command, player, requestId);
            CommandResultPublisher.Publish(
                player.Project,
                result,
                player.Client?.ID,
                includeServer: player.IsMainPlayer);
            return requestId;
        }

        if (CurrentModRuntime.Value is not { } runtime)
        {
            PublishLocalFailure(
                player,
                CommandResult.LocalizedFail(
                    "command.unavailable",
                    "CommandUnavailable_Message",
                    "命令系统尚未就绪。"));
            return requestId;
        }

        if (!runtime.Commands.TryEncode(
                command,
                out var commandId,
                out var payload,
                out var error))
        {
            PublishLocalFailure(
                player,
                CommandResult.Fail("command.not_remote", error));
            return requestId;
        }

        CommonLib.Net.QueuePackage(
            CommandPackage.CreateRequest(commandId, payload, requestId));
        return requestId;
    }

    private static void PublishLocalFailure(
        PlayerData player,
        CommandResult result)
    {
        CommandResultPublisher.Publish(
            player.Project,
            result,
            includeServer: true);
    }
}
