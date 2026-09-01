using System.Collections.Concurrent;

using Game.Network;

namespace Game.Commands;

/// <summary>
///     Moves HTTP command requests from listener threads onto the game update
///     thread. Other command frontends execute through their own dispatch paths.
/// </summary>
public static class HttpCommandExecutionQueue
{
    private sealed record Request(
        HttpCommandRequest Command,
        CommandPrincipal Principal,
        string CorrelationId,
        TaskCompletionSource<CommandResult> Completion);

    private static readonly ConcurrentQueue<Request> _requests = new();

    private sealed record DiscoveryRequest(
        CommandPrincipal Principal,
        TaskCompletionSource<IReadOnlyList<HttpDiscoveredCommand>> Completion);

    private static readonly ConcurrentQueue<DiscoveryRequest> _discoveryRequests = new();

    public static Task<CommandResult> SubmitAsync(
        HttpCommandRequest command,
        CommandPrincipal principal,
        string correlationId)
    {
        var completion = new TaskCompletionSource<CommandResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _requests.Enqueue(new Request(command, principal, correlationId, completion));
        return completion.Task;
    }

    public static Task<IReadOnlyList<HttpDiscoveredCommand>> DiscoverAsync(CommandPrincipal principal)
    {
        var completion = new TaskCompletionSource<IReadOnlyList<HttpDiscoveredCommand>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _discoveryRequests.Enqueue(new DiscoveryRequest(principal, completion));
        return completion.Task;
    }

    public static void Update()
    {
        while (_requests.TryDequeue(out var request))
        {
            try
            {
                if (CurrentModRuntime.Value is not { } runtime)
                {
                    request.Completion.TrySetResult(CommandResult.Fail(
                        "command.unavailable",
                        "Command system is not ready."));
                    continue;
                }

                var context = new CommandContext(
                    CommandInvocationChannel.HttpApi,
                    request.Principal,
                    GameManager.Project,
                    request.CorrelationId);
                request.Completion.TrySetResult(
                    new HttpCommandAdapter(runtime.Commands).Execute(request.Command, context));
            }
            catch (Exception exception)
            {
                Log.Error($"HTTP command execution failed: {exception}");
                request.Completion.TrySetResult(CommandResult.Fail(
                    "command.internal_error",
                    "Command execution failed."));
            }
        }

        while (_discoveryRequests.TryDequeue(out var request))
        {
            try
            {
                if (CurrentModRuntime.Value is not { } runtime)
                {
                    request.Completion.TrySetException(new InvalidOperationException(
                        "Command system is not ready."));
                    continue;
                }

                var project = GameManager.Project;
                var commands = runtime.Commands.Adapters.Get<HttpCommandBinding>()
                    .Where(entry => runtime.Commands.TryGetDefinition(entry.Id, out var command) &&
                                    command is not null &&
                                    command.Definition.CanInvoke(request.Principal, project) &&
                                    command.Definition.CanExecuteHere(RunMode.Value, CommonLib.WorkType) &&
                                    command.Definition.IsPotentiallyAuthorized(
                                        runtime.Commands.Permissions,
                                        request.Principal,
                                        project))
                    .Select(entry =>
                    {
                        runtime.Commands.TryGetDefinition(entry.Id, out var command);
                        return new HttpDiscoveredCommand(
                            entry.Id.ToString(),
                            command!.Definition.Description.Resolve(),
                            entry.Binding.Arguments);
                    })
                    .ToArray();
                request.Completion.TrySetResult(commands);
            }
            catch (Exception exception)
            {
                Log.Error($"HTTP command discovery failed: {exception}");
                request.Completion.TrySetException(exception);
            }
        }
    }

    public static void FailPending()
    {
        while (_requests.TryDequeue(out var request))
        {
            request.Completion.TrySetResult(CommandResult.Fail(
                "command.unavailable",
                "Command host is stopping."));
        }

        while (_discoveryRequests.TryDequeue(out var request))
        {
            request.Completion.TrySetException(new InvalidOperationException(
                "Command host is stopping."));
        }
    }
}
