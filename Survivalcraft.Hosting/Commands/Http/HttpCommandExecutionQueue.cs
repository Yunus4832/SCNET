using System.Collections.Concurrent;

using Game.Localization;

namespace Game.Commands.Http;

/// <summary>在宿主循环上接收 HTTP 请求，将世界和服务器命令路由到对应权威端。应用命令在宿主本地处理，其他前端使用各自的分发路径。</summary>
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

                if (scope.Registry.TryGetDefinition(request.Command.Identity, out var definition) &&
                    definition?.Definition.Domain == CommandDomain.Application)
                {
                    request.Completion.TrySetResult(Execute(request, scope.Registry, scope.ApplicationProject));
                }
                else if (scope.Authority is { } authority)
                {
                    _ = CompleteAuthorityAsync(
                        request,
                        authority.InvokeAsync(
                            world =>
                                Execute(request, scope.Registry, world.Project),
                            request.Cancellation
                        )
                    );
                }
                else { request.Completion.TrySetResult(Execute(request, scope.Registry, null)); }
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

                var resolver = getResolver();
                var commands = new HttpCommandAdapter(scope.Registry).Discover(
                    request.Principal,
                    scope.ApplicationProject,
                    null,
                    resolver,
                    CommandDomain.Application
                );
                if (scope.Authority is { } authority)
                {
                    _ = CompleteDiscoveryAsync(
                        request,
                        commands,
                        authority.InvokeAsync(
                            world =>
                                new HttpCommandAdapter(scope.Registry).Discover(
                                    request.Principal,
                                    null,
                                    world.Project,
                                    resolver,
                                    CommandDomain.World,
                                    CommandDomain.Server
                                ),
                            request.Cancellation
                        )
                    );
                }
                else { request.Completion.TrySetResult(commands); }
            }
            catch (Exception exception)
            {
                Log.Error($"HTTP command discovery failed: {exception}");
                request.Completion.TrySetException(exception);
            }
        }
    }

    private static CommandResult Execute(Request request, CommandRegistry registry, EntitySystem.Core.Project? project)
    {
        var context = new CommandContext(
            CommandInvocationChannel.HttpApi,
            request.Principal,
            project,
            request.CorrelationId
        );
        return new HttpCommandAdapter(registry).Execute(request.Command, context);
    }

    private static async Task CompleteAuthorityAsync(Request request, Task<CommandResult> operation)
    {
        try { request.Completion.TrySetResult(await operation.ConfigureAwait(false)); }
        catch (OperationCanceledException) { request.Completion.TrySetCanceled(request.Cancellation); }
        catch (Exception error)
        {
            Log.Error($"Authority HTTP command failed: {error}");
            request.Completion.TrySetResult(CommandResult.Fail("command.internal_error", "Command execution failed."));
        }
    }

    private static async Task CompleteDiscoveryAsync(
        DiscoveryRequest request,
        IReadOnlyList<HttpDiscoveredCommand> application,
        Task<IReadOnlyList<HttpDiscoveredCommand>> operation
    )
    {
        try
        {
            var authority = await operation.ConfigureAwait(false);
            request.Completion.TrySetResult(
                application.Concat(authority).OrderBy(
                    command => command.Identity,
                    StringComparer.Ordinal
                ).ToArray()
            );
        }
        catch (OperationCanceledException) { request.Completion.TrySetCanceled(request.Cancellation); }
        catch (Exception error) { request.Completion.TrySetException(error); }
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
