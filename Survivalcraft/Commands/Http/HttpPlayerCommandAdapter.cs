using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

using Engine.Core;

using Game.Network;
using Game.Network.Enums;

namespace Game.Commands;

/// <summary>GUI HTTP requests bound to the current player, never to a supplied identity.</summary>
public static class HttpPlayerCommandAdapter
{
    private sealed record Request(
        HttpCommandRequest? Command,
        TaskCompletionSource<CommandResult> Completion,
        CancellationToken Cancellation);

    private sealed record Pending(
        PlayerData Player,
        Client Server,
        Request Request);

    private static readonly ConcurrentQueue<Request> _requests = new();
    private static readonly ConcurrentDictionary<string, Pending> _pending = new();
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public static Task<CommandResult> SubmitAsync(
        HttpCommandRequest? command,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _requests.Enqueue(new Request(command, completion, cancellationToken));
        return completion.Task.WaitAsync(cancellationToken);
    }

    internal static CommandResult Execute(
        CommandRegistry registry,
        HttpCommandRequest? request,
        PlayerData player,
        string correlationId)
    {
        var principal = CommandPrincipal.FromPlayer(player);
        var adapter = new HttpCommandAdapter(registry);
        if (request is null)
        {
            return CommandResult.SilentOk() with
            {
                Data = new JsonObject
                {
                    ["commands"] = JsonSerializer.SerializeToNode(
                        adapter.Discover(principal, player.Project, playerScope: true), _jsonOptions)
                }
            };
        }

        if (registry.TryGetDefinition(request.Identity, out var command) &&
            command?.Definition.Domain is CommandDomain.Application)
        {
            return CommandResult.Fail("command.player_scope", "Application commands use the local control endpoint.");
        }

        return adapter.Execute(request, new CommandContext(
            CommandInvocationChannel.HttpApi, principal, player.Project, correlationId));
    }

    public static void Update()
    {
        foreach (var (id, pending) in _pending)
        {
            if (pending.Request.Cancellation.IsCancellationRequested || !IsCurrent(pending))
            {
                if (_pending.TryRemove(id, out _))
                {
                    pending.Request.Completion.TrySetResult(CommandResult.Fail(
                        "command.player_changed", "The player or server connection is no longer active."));
                }
            }
        }

        while (_requests.TryDequeue(out var request))
        {
            if (request.Cancellation.IsCancellationRequested)
            {
                request.Completion.TrySetCanceled(request.Cancellation);
                continue;
            }

            if (RunMode.Value is not RunModeType.Gui ||
                CommonLib.MainPlayer?.PlayerData is not { } player ||
                !ReferenceEquals(player.Project, GameManager.Project))
            {
                request.Completion.TrySetResult(CommandResult.Fail(
                    "command.no_player", "No active GUI player is available."));
                continue;
            }

            var id = Guid.NewGuid().ToString("N");
            if (CommonLib.WorkType is WorkType.Client)
            {
                if (CommonLib.Net.Server is not { IsConnected: true } server)
                {
                    request.Completion.TrySetResult(CommandResult.Fail("command.disconnected", "The server is disconnected."));
                    continue;
                }

                _pending[id] = new Pending(player, server, request);
            }

            try
            {
                var result = CommandGateway.SubmitPlayerHttp(player, request.Command, id);
                if (result is not null)
                {
                    _pending.TryRemove(id, out _);
                    request.Completion.TrySetResult(result);
                }
            }
            catch (Exception exception)
            {
                _pending.TryRemove(id, out _);
                Log.Error($"Player HTTP command failed: {exception}");
                request.Completion.TrySetResult(CommandResult.Fail("command.failed", "Player command failed."));
            }
        }
    }

    internal static void Receive(string correlationId, CommandResult result)
    {
        if (_pending.TryRemove(correlationId, out var pending))
        {
            pending.Request.Completion.TrySetResult(IsCurrent(pending)
                ? result
                : CommandResult.Fail("command.player_changed", "The player or server connection changed."));
        }
    }

    private static bool IsCurrent(Pending pending)
    {
        return RunMode.Value is RunModeType.Gui &&
               ReferenceEquals(CommonLib.MainPlayer?.PlayerData, pending.Player) &&
               ReferenceEquals(GameManager.Project, pending.Player.Project) &&
               CommonLib.WorkType is WorkType.Client &&
               ReferenceEquals(CommonLib.Net.Server, pending.Server) &&
               pending.Server.IsConnected;
    }

    public static void FailPending()
    {
        var failure = CommandResult.Fail("command.unavailable", "Command host is stopping.");
        while (_requests.TryDequeue(out var request))
        {
            request.Completion.TrySetResult(failure);
        }

        foreach (var id in _pending.Keys)
        {
            if (_pending.TryRemove(id, out var pending))
            {
                pending.Request.Completion.TrySetResult(failure);
            }
        }
    }
}
