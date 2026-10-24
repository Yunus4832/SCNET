using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

using Game.Localization;
using Game.Network;

namespace Game.Commands;

/// <summary>GUI HTTP requests bound to the current player, never to a supplied identity.</summary>
public static class HttpPlayerCommandAdapter
{
    private sealed record Request(
        HttpCommandRequest? Command,
        TaskCompletionSource<CommandResult> Completion,
        CancellationToken Cancellation
    );

    private sealed record Pending(
        CharacterData Character,
        Client Server,
        CommandRegistry Registry,
        Request Request
    );

    private static readonly ConcurrentQueue<Request> _requests = new();

    private static readonly ConcurrentDictionary<string, Pending> _pending = new();

    // Retain recent local routes so canceled or duplicate responses never become UI notifications.
    private static readonly ConcurrentDictionary<string, byte> _responseRoutes = new();

    private static readonly ConcurrentQueue<string> _responseOrder = new();

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public static Task<CommandResult> SubmitAsync(
        HttpCommandRequest? command,
        CancellationToken cancellationToken
    )
    {
        var completion = new TaskCompletionSource<CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _requests.Enqueue(new Request(command, completion, cancellationToken));
        return completion.Task.WaitAsync(cancellationToken);
    }

    internal static bool TryCreateCommand(
        CommandRegistry registry,
        HttpCommandRequest? request,
        out IGameCommand? command,
        out CommandResult? error
    )
    {
        if (request is null)
        {
            command = new GetCommandCatalogCommand();
            error = null;
            return true;
        }

        if (registry.TryGetDefinition(request.Identity, out var registered) &&
            registered?.Definition.Domain is CommandDomain.Application)
        {
            command = null;
            error = CommandResult.Fail("command.player_scope", "Application commands use the local control endpoint.");
            return false;
        }

        return new HttpCommandAdapter(registry).TryCreate(request, out command, out error);
    }

    private static CommandResult Complete(Request request, CommandRegistry registry, CommandResult result)
    {
        if (request.Command is not null || !result.Success)
        {
            return result;
        }

        var identities = result.Data?["commands"]?.AsArray()
                             .Select(value => value!.GetValue<string>()).ToHashSet(StringComparer.Ordinal) ??
                         [];
        var commands = registry.Adapters.Get<HttpCommandBinding>()
            .Where(entry => identities.Contains(entry.Id.ToString()) &&
                            registry.TryGetDefinition(entry.Id, out var registered) &&
                            registered!.Definition.Domain is not CommandDomain.Application &&
                            (CommonLib.WorkType is not WorkType.Client ||
                             registered.Definition.SupportsRemoteInvocation)
            )
            .Select(entry =>
                {
                    registry.TryGetDefinition(entry.Id, out var registered);
                    return new HttpDiscoveredCommand(
                        entry.Id.ToString(),
                        registered!.Definition.Description.Resolve(),
                        entry.Binding.Arguments
                    );
                }
            ).ToArray();
        return result with
        {
            Data = new JsonObject { ["commands"] = JsonSerializer.SerializeToNode(commands, _jsonOptions) }
        };
    }

    public static void Update()
    {
        foreach (var (id, pending) in _pending)
        {
            if (pending.Request.Cancellation.IsCancellationRequested || !IsCurrent(pending))
            {
                if (_pending.TryRemove(id, out _))
                {
                    pending.Request.Completion.TrySetResult(
                        CommandResult.Fail(
                            "command.player_changed",
                            "The player or server connection is no longer active."
                        )
                    );
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
                CommonLib.LocalCharacter?.CharacterData is not { } player ||
                !ReferenceEquals(player.Project, GameManager.Project))
            {
                request.Completion.TrySetResult(
                    CommandResult.Fail("command.no_player", "No active GUI player is available.")
                );
                continue;
            }

            if (CurrentModRuntime.Value is not { } runtime)
            {
                request.Completion.TrySetResult(
                    CommandResult.Fail(
                        "command.unavailable",
                        "Command system is not ready."
                    )
                );
                continue;
            }

            if (!TryCreateCommand(runtime.Commands, request.Command, out var command, out var error))
            {
                request.Completion.TrySetResult(error!);
                continue;
            }

            var id = Guid.NewGuid().ToString("N");
            if (CommonLib.WorkType is WorkType.Client)
            {
                if (CommonLib.Net.Server is not { IsConnected: true } server)
                {
                    request.Completion.TrySetResult(
                        CommandResult.Fail(
                            "command.disconnected",
                            "The server is disconnected."
                        )
                    );
                    continue;
                }

                _pending[id] = new Pending(player, server, runtime.Commands, request);
                _responseRoutes[id] = 0;
                _responseOrder.Enqueue(id);
                while (_responseOrder.Count > 1024 && _responseOrder.TryDequeue(out var expired))
                {
                    _responseRoutes.TryRemove(expired, out _);
                }
            }

            try
            {
                var result = CommandGateway.ExecuteOrSend(player, command!, id);
                if (result is not null)
                {
                    _pending.TryRemove(id, out _);
                    request.Completion.TrySetResult(Complete(request, runtime.Commands, result));
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

    internal static bool TryReceive(string correlationId, CommandResult result)
    {
        if (_pending.TryRemove(correlationId, out var pending))
        {
            pending.Request.Completion.TrySetResult(
                IsCurrent(pending)
                    ? Complete(pending.Request, pending.Registry, result)
                    : CommandResult.Fail("command.player_changed", "The player or server connection changed.")
            );
            return true;
        }

        return _responseRoutes.ContainsKey(correlationId);
    }

    private static bool IsCurrent(Pending pending)
    {
        return RunMode.Value is RunModeType.Gui &&
               ReferenceEquals(CommonLib.LocalCharacter?.CharacterData, pending.Character) &&
               ReferenceEquals(GameManager.Project, pending.Character.Project) &&
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
