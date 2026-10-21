using EntitySystem.Core;

using Game.Network;
using Game.Network.Transport;

namespace Game.Commands;

/// <summary>
///     Routes a player command to the local authoritative server or to the remote
///     server without exposing transport details to UI adapters.
/// </summary>
public static class CommandGateway
{
    public static string SubmitLocal(
        Project project,
        IGameCommand command,
        string? correlationId = null
    )
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(command);
        var requestId = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId;
        var result = CommandExecutor.ExecuteLocal(
            command,
            project,
            requestId
        );
        CommandResultPublisher.Publish(project, result);
        return requestId;
    }

    public static string Submit(
        PlayerData player,
        string input,
        string? correlationId = null
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        var requestId = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId;
        if (CurrentModRuntime.Value is not { } runtime)
        {
            CommandResultPublisher.DisplayLocal(
                player.Project,
                CommandResult.Fail("command.unavailable", "Command system is not ready."),
                requestId
            );
            return requestId;
        }

        var adapter = new TextCommandAdapter(runtime.Commands);
        var principal = adapter.SupportsDomain(input, CommandDomain.Application)
            ? CommandPrincipal.LocalOperator
            : CommandPrincipal.FromPlayer(player);
        var context = new CommandContext(CommandInvocationChannel.Text, principal, player.Project, requestId);
        if (!adapter.TryCreate(input, context, out var command, out var error))
        {
            CommandResultPublisher.DisplayLocal(player.Project, error!, requestId);
            return requestId;
        }

        return Submit(player, command!, requestId);
    }

    public static string Submit(
        PlayerData player,
        IGameCommand command,
        string? correlationId = null
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(command);
        var requestId = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId;
        var result = ExecuteOrSend(player, command, requestId);
        if (result is not null)
        {
            if (CurrentModRuntime.Value is { } runtime &&
                runtime.Commands.TryGetDefinition(command.GetType(), out var registered) &&
                registered?.Definition.Domain is CommandDomain.Application)
            {
                CommandResultPublisher.DisplayLocal(player.Project, result, requestId);
            }
            else
            {
                CommandResultPublisher.Publish(
                    player.Project,
                    result,
                    player.Client?.ID,
                    includeServer: player.IsMainPlayer,
                    correlationId: requestId
                );
            }
        }

        return requestId;
    }

    internal static CommandResult? ExecuteOrSend(PlayerData player, IGameCommand command, string correlationId)
    {
        if (CurrentModRuntime.Value is not { } runtime)
        {
            return CommandResult.Fail("command.unavailable", "Command system is not ready.");
        }

        if (runtime.Commands.TryGetDefinition(command.GetType(), out var registered) &&
            registered?.Definition.Domain is CommandDomain.Application)
        {
            return CommandExecutor.ExecuteLocal(command, player.Project, correlationId);
        }

        if (CommonLib.WorkType is WorkType.Local or WorkType.Server)
        {
            return CommandExecutor.ExecutePlayer(command, player, correlationId);
        }

        if (!TryCreateRequest(runtime.Commands, command, correlationId, out var package, out var error))
        {
            return error;
        }

        NetworkSender.SendToServer(package!);
        return null;
    }

    internal static bool TryCreateRequest(
        CommandRegistry registry,
        IGameCommand command,
        string correlationId,
        out CommandPackage? package,
        out CommandResult? error
    )
    {
        if (!registry.TryEncode(command, out var commandId, out var payload, out var reason))
        {
            package = null;
            error = CommandResult.Fail("command.not_remote", reason);
            return false;
        }

        if (payload.Length > ProtocolLimits.MaximumCommandPayloadLength)
        {
            package = null;
            error = CommandResult.LocalizedFail("command.too_long", "CommandTooLong_Message", "指令长度超过限制。");
            return false;
        }

        package = CommandPackage.CreateRequest(commandId, payload, correlationId);
        error = null;
        return true;
    }
}
