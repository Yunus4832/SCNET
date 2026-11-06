using System.Collections.Concurrent;

using Game.Localization;

namespace Game.Commands.Http;

/// <summary>
///     Moves HTTP command requests from listener threads onto the game update
///     thread. Other command frontends execute through their own dispatch paths.
/// </summary>
public sealed class HttpCommandExecutionQueue(
    Func<CommandExecutionScope?> getScope,
    Func<LocalizedTextResolver> getResolver
)
{
    private sealed record Request(
        HttpCommandRequest Command,
        CommandPrincipal Principal,
        string CorrelationId,
        CancellationToken Cancellation,
        TaskCompletionSource<CommandResult> Completion
    );

    private readonly ConcurrentQueue<Request> _requests = new();

    private sealed record DiscoveryRequest(
        CommandPrincipal Principal,
        CancellationToken Cancellation,
        TaskCompletionSource<IReadOnlyList<HttpDiscoveredCommand>> Completion
    );

    private readonly ConcurrentQueue<DiscoveryRequest> _discoveryRequests = new();

    public Task<CommandResult> SubmitAsync(
        HttpCommandRequest command,
        CommandPrincipal principal,
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        var completion = new TaskCompletionSource<CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _requests.Enqueue(new Request(command, principal, correlationId, cancellationToken, completion));
        return completion.Task.WaitAsync(cancellationToken);
    }

    public Task<IReadOnlyList<HttpDiscoveredCommand>> DiscoverAsync(
        CommandPrincipal principal,
        CancellationToken cancellationToken = default
    )
    {
        var completion = new TaskCompletionSource<IReadOnlyList<HttpDiscoveredCommand>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _discoveryRequests.Enqueue(new DiscoveryRequest(principal, cancellationToken, completion));
        return completion.Task.WaitAsync(cancellationToken);
    }

    public void Update()
    {
        while (_requests.TryDequeue(out var request))
        {
            try
            {
                if (request.Cancellation.IsCancellationRequested)
                {
                    request.Completion.TrySetCanceled(request.Cancellation);
                    continue;
                }

                if (getScope() is not { } scope)
                {
                    request.Completion.TrySetResult(
                        CommandResult.Fail(
                            "command.unavailable",
                            "Command system is not ready."
                        )
                    );
                    continue;
                }

                var project = scope.Registry.TryGetDefinition(request.Command.Identity, out var definition) &&
                              definition?.Definition.Domain == CommandDomain.Application
                    ? scope.ApplicationProject
                    : scope.AuthorityProject;
                var context = new CommandContext(
                    CommandInvocationChannel.HttpApi,
                    request.Principal,
                    project,
                    request.CorrelationId
                );
                request.Completion.TrySetResult(
                    new HttpCommandAdapter(scope.Registry).Execute(request.Command, context)
                );
            }
            catch (Exception exception)
            {
                Log.Error($"HTTP command execution failed: {exception}");
                request.Completion.TrySetResult(
                    CommandResult.Fail(
                        "command.internal_error",
                        "Command execution failed."
                    )
                );
            }
        }

        while (_discoveryRequests.TryDequeue(out var request))
        {
            try
            {
                if (request.Cancellation.IsCancellationRequested)
                {
                    request.Completion.TrySetCanceled(request.Cancellation);
                    continue;
                }

                if (getScope() is not { } scope)
                {
                    request.Completion.TrySetException(new InvalidOperationException("Command system is not ready."));
                    continue;
                }

                var commands = new HttpCommandAdapter(scope.Registry)
                    .Discover(
                        request.Principal,
                        scope.ApplicationProject,
                        scope.AuthorityProject,
                        getResolver()
                    );
                request.Completion.TrySetResult(commands);
            }
            catch (Exception exception)
            {
                Log.Error($"HTTP command discovery failed: {exception}");
                request.Completion.TrySetException(exception);
            }
        }
    }

    public void FailPending()
    {
        while (_requests.TryDequeue(out var request))
        {
            request.Completion.TrySetResult(
                CommandResult.Fail(
                    "command.unavailable",
                    "Command host is stopping."
                )
            );
        }

        while (_discoveryRequests.TryDequeue(out var request))
        {
            request.Completion.TrySetException(new InvalidOperationException("Command host is stopping."));
        }
    }
}
