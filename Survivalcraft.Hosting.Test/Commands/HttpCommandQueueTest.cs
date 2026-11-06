using Game.Commands;
using Game.Commands.Http;
using Game.Localization;
using Game.Protocol;

namespace Survivalcraft.Hosting.Test.Commands;

public sealed class HttpCommandQueueTest
{
    [Fact]
    public async Task QueuesHonorCancellationAndExecuteOnlyOnTheirOwnUpdate()
    {
        var registry = new CommandRegistry();
        var identity = ResourceId.Parse("test:queued");
        var executions = 0;
        registry.Register(
            new ModId("test"),
            identity,
            new CommandDefinition<QueuedCommand>(
                (_, _) =>
                {
                    executions++;
                    return CommandResult.Ok("executed");
                },
                CommandDomain.Application
            )
        );
        registry.Adapters.Register(
            new ModId("test"),
            identity,
            HttpCommandBinding.Create<QueuedCommand>(_ => new QueuedCommand())
        );
        registry.Freeze();
        var resolver = new LocalizedTextResolver(new LanguageCatalog());
        var first = new HttpCommandExecutionQueue(
            () => new CommandExecutionScope(registry, null, null),
            () => resolver
        );
        var second = new HttpCommandExecutionQueue(
            () => new CommandExecutionScope(registry, null, null),
            () => resolver
        );
        var request = new HttpCommandRequest(identity, new System.Text.Json.Nodes.JsonObject());
        var canceled = first.SubmitAsync(request, CommandPrincipal.LocalOperator, "first");
        using var deadline = new CancellationTokenSource();
        var expired = second.SubmitAsync(request, CommandPrincipal.LocalOperator, "expired", deadline.Token);
        var expiredDiscovery = second.DiscoverAsync(CommandPrincipal.LocalOperator, deadline.Token);
        var pending = second.SubmitAsync(request, CommandPrincipal.LocalOperator, "second");
        await deadline.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => expired);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => expiredDiscovery);
        Assert.False(pending.IsCompleted);
        Assert.Equal(0, executions);
        first.FailPending();
        Assert.Equal("command.unavailable", (await canceled).Code);
        Assert.False(pending.IsCompleted);
        second.Update();
        Assert.True((await pending).Success);
        Assert.Equal(1, executions);
    }

    private sealed record QueuedCommand : IGameCommand;
}
