using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

using Game.Commands.BuiltIn;
using Game.Localization;

namespace Game.Commands.Http;

/// <summary>GUI HTTP requests bound to the current player, never to a supplied identity.</summary>
public sealed class HttpPlayerCommandAdapter(
    Func<CharacterData?> getPlayer,
    Func<CommandRegistry?> getRegistry,
    Func<LocalizedTextResolver> getResolver,
    Func<CharacterData, IGameCommand, string, CommandResult?> execute
)
{
    private sealed record Request(
        HttpCommandRequest? Command,
        TaskCompletionSource<CommandResult> Completion,
        CancellationToken Cancellation
    );

    private sealed record Pending(
        CharacterData Character,
        WorldConnection Server,
        CommandRegistry Registry,
        Request Request
    );

    private readonly ConcurrentQueue<Request> _requests = new();

    private readonly ConcurrentDictionary<string, Pending> _pending = new();

    // Retain recent local routes so canceled or duplicate responses never become UI notifications.
    private readonly ConcurrentDictionary<string, byte> _responseRoutes = new();

    private readonly ConcurrentQueue<string> _responseOrder = new();

    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public Task<CommandResult> SubmitAsync(
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

    private CommandResult Complete(Request request, CommandRegistry registry, CommandResult result)
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
                            registered.Definition.SupportsRemoteInvocation
            )
            .Select(entry =>
                {
                    registry.TryGetDefinition(entry.Id, out var registered);
                    return new HttpDiscoveredCommand(
                        entry.Id.ToString(),
                        getResolver().Resolve(registered!.Definition.Description),
                        entry.Binding.Arguments
                    );
                }
            ).ToArray();
        return result with
        {
            Data = new JsonObject { ["commands"] = JsonSerializer.SerializeToNode(commands, _jsonOptions) }
        };
    }

    public void Update()
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

            if (getPlayer() is not { } player)
            {
                request.Completion.TrySetResult(
                    CommandResult.Fail("command.no_player", "No active GUI player is available.")
                );
                continue;
            }

            if (getRegistry() is not { } registry)
            {
                request.Completion.TrySetResult(
                    CommandResult.Fail(
                        "command.unavailable",
                        "Command system is not ready."
                    )
                );
                continue;
            }

            if (!TryCreateCommand(registry, request.Command, out var command, out var error))
            {
                request.Completion.TrySetResult(error!);
                continue;
            }

            var id = Guid.NewGuid().ToString("N");
            var node = WorldNetworkBinding.For(player.Project).RequireNode();
            if (!node.Runtime.IsAuthority)
            {
                if (node.Server is not { IsConnected: true } server)
                {
                    request.Completion.TrySetResult(
                        CommandResult.Fail(
                            "command.disconnected",
                            "The server is disconnected."
                        )
                    );
                    continue;
                }

                _pending[id] = new Pending(player, server, registry, request);
                _responseRoutes[id] = 0;
                _responseOrder.Enqueue(id);
                while (_responseOrder.Count > 1024 && _responseOrder.TryDequeue(out var expired))
                {
                    _responseRoutes.TryRemove(expired, out _);
                }
            }

            try
            {
                var result = execute(player, command!, id);
                if (result is not null)
                {
                    _pending.TryRemove(id, out _);
                    request.Completion.TrySetResult(Complete(request, registry, result));
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

    public bool TryReceive(string correlationId, CommandResult result)
    {
        if (_pending.TryRemove(correlationId, out var pending))
        {
            pending.Request.Completion.TrySetResult(
                IsCurrent(pending) && !pending.Request.Cancellation.IsCancellationRequested
                    ? Complete(pending.Request, pending.Registry, result)
                    : CommandResult.Fail("command.player_changed", "The player or server connection changed.")
            );
            return true;
        }

        return _responseRoutes.ContainsKey(correlationId);
    }

    private bool IsCurrent(Pending pending)
    {
        return ReferenceEquals(getRegistry(), pending.Registry) &&
               ReferenceEquals(getPlayer(), pending.Character) &&
               !WorldRuntimeContext.For(pending.Character.Project).IsAuthority &&
               ReferenceEquals(
                   WorldNetworkBinding.FindNode(pending.Character.Project)?.Server,
                   pending.Server
               ) &&
               pending.Server.IsConnected;
    }

    public void FailPending()
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
