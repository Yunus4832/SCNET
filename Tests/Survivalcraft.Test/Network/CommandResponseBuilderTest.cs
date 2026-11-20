using System.Text.Json.Nodes;

using Game.Commands;

namespace Survivalcraft.Test.Network;

public sealed class CommandResponseBuilderTest
{
    [Fact]
    public void BusinessResultsAndPoliciesBelongToTheGame()
    {
        foreach (var type in new[]
                 {
                     typeof(CommandResult), typeof(CommandResultAudience), typeof(CommandResultState),
                     typeof(CommandResultPresentation), typeof(CommandPanelBehavior)
                 })
        {
            Assert.Equal(typeof(CommandDispatcher).Assembly, type.Assembly);
            Assert.Equal("Game.Commands", type.Namespace);
            Assert.Null(typeof(GameProtocol).Assembly.GetType("Game.Protocol.Commands." + type.Name));
        }
    }

    [Fact]
    public void CaptureFreezesMutableDataBeforeQueueing()
    {
        var data = new JsonObject { ["value"] = 1 };
        var arguments = new[] { LocalizedText.Literal("before") };
        var result = new CommandResult(
            true,
            "test",
            LocalizedText.Format(LocalizedText.Literal("test {0}"), arguments),
            Data: data
        );
        var response = CommandResponseBuilder.Capture(result);
        data["value"] = 2;
        arguments[0] = LocalizedText.Literal("after");
        Assert.Equal(1, response.Data!["value"]!.GetValue<int>());
        Assert.Equal("before", Assert.Single(response.Message.Arguments).Fallback);
    }

    [Fact]
    public void RepliesKeepCompletionAndLocalizationWithoutPublishingPolicies()
    {
        var result = CommandResult.LocalizedPublicToast("pending", "key", "hello {0}", "Alice") with
        {
            Sensitive = true,
            State = CommandResultState.Pending,
            PanelBehavior = CommandPanelBehavior.CloseOnSuccess
        };
        var response = CommandResponseBuilder.Capture(result);
        Assert.True(response.IsPending);
        Assert.Equal("key", response.Message.Template!.Key);
        Assert.Equal("Alice", Assert.Single(response.Message.Arguments).Fallback);
        Assert.Equal(GameMessagePresentation.Toast, response.Presentation);
        Assert.True(response.CloseInputOnSuccess);
        var restored = CommandResponseBuilder.Restore(response);
        Assert.Equal(CommandResultState.Pending, restored.State);
        Assert.Equal(result.Message, restored.Message);
        Assert.Equal(CommandResultAudience.Requester, restored.Audience);
        Assert.Equal(CommandResultPresentation.Toast, restored.Presentation);
        Assert.Equal(CommandPanelBehavior.CloseOnSuccess, restored.PanelBehavior);
    }
}
