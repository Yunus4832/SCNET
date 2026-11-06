using Game.Commands;
using Game.Protocol;
using Game.World;

namespace Survivalcraft.Hosting.Test.Commands;

public sealed class CommandKernelBoundaryTest
{
    [Fact]
    public void EncodeAndDecodeFailuresKeepStructuredRootCausesWithoutResolvingAnyLocale()
    {
        var registry = new CommandRegistry();
        Assert.False(registry.TryEncode(new MissingCommand(), out _, out _, out var encode));
        Assert.NotNull(encode);
        Assert.Equal("CommandTypeUnregistered_Message", encode.Message.Template!.Key);
        Assert.Equal([nameof(MissingCommand)], encode.Message.Arguments.Select(argument => argument.Fallback));
        Assert.False(encode.Success);
        Assert.False(registry.TryDecode(ResourceId.Parse("test:missing"), [], out _, out var decode));
        Assert.NotNull(decode);
        Assert.Equal("CommandIdentityUnknown_Message", decode.Message.Template!.Key);
        Assert.Equal(["test:missing"], decode.Message.Arguments.Select(argument => argument.Fallback));
        Assert.False(decode.Success);
    }

    [Fact]
    public void LocalCompletionIsWorldScopedAndCarriesTheUnchangedCorrelationAndResult()
    {
        var firstWorld = new WorldRuntimeContext(RuntimeSide.Client, false);
        var otherWorld = new WorldRuntimeContext(RuntimeSide.Client, false);
        using var first = new WorldCommandResults(firstWorld);
        using var other = new WorldCommandResults(otherWorld);
        var received = new List<CommandResultDelivery>();
        var unaddressed = 0;
        first.Delivered += received.Add;
        other.Delivered += _ => unaddressed++;
        var result = CommandResult.Pending("pending") with { PanelBehavior = CommandPanelBehavior.CloseOnSuccess };

        first.Publish(result, "real-route");

        var delivery = Assert.Single(received);
        Assert.Same(result, delivery.Result);
        Assert.Equal("real-route", delivery.CorrelationId);
        Assert.Equal(0, unaddressed);
        firstWorld.Close();
        first.Publish(CommandResult.Ok("late"), "late-route");
        Assert.Single(received);
        Assert.Throws<InvalidOperationException>(() => WorldCommandResults.For(firstWorld));
        Assert.Same(other, WorldCommandResults.For(otherWorld));
        otherWorld.Close();
    }

    [Fact]
    public void ACompletionOwnerCannotBeSharedOrRecreatedAfterItsWorldCloses()
    {
        var world = new WorldRuntimeContext(RuntimeSide.Authority, false);
        using var results = new WorldCommandResults(world);
        Assert.Throws<ArgumentException>(() => new WorldCommandResults(world));
        world.Close();
        results.Dispose();
        results.Dispose();
        Assert.Throws<ObjectDisposedException>(() => new WorldCommandResults(world));
    }

    private sealed record MissingCommand : IGameCommand;
}
