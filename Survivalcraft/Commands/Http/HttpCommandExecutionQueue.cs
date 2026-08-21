using System.Collections.Concurrent;

namespace Game.Commands;

/// <summary>
/// Moves HTTP command requests from listener threads onto the game update
/// thread. Other command frontends execute through their own dispatch paths.
/// </summary>
public static class HttpCommandExecutionQueue
{
    private sealed record Request(
        HttpCommandRequest Command,
        CommandPrincipal Principal,
        string CorrelationId,
        TaskCompletionSource<CommandResult> Completion);

    private static readonly ConcurrentQueue<Request> _requests = new();

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
    }

    public static void FailPending()
    {
        while (_requests.TryDequeue(out var request))
        {
            request.Completion.TrySetResult(CommandResult.Fail(
                "command.unavailable",
                "Command host is stopping."));
        }
    }
}
