using Game.Commands;
using Game.Localization;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public class CommandPackageTest
{
    [Fact]
    public void SuggestionsPreserveRequestIdentityAndLocalizationWithoutExecutingCommands()
    {
        var request = RoundTrip(CommandPackage.CreateSuggestionRequest("/tp self private H", "completion-1"));
        Assert.Equal(CommandPackage.CommandPackageMode.SuggestionRequest, request.Mode);
        Assert.Equal("/tp self private H", request.Input);
        Assert.Equal("completion-1", request.CorrelationId);
        Assert.Null(request.Result);
        var description = new LocalizedText("Commands", "TeleportMark_Description", "Teleport to a mark");
        var response = RoundTrip(
            CommandPackage.CreateSuggestions(
                request.Input,
                request.CorrelationId,
                [new CommandSuggestion("Home One", description.Resolve(), true, description)]
            )
        );
        Assert.Equal(CommandPackage.CommandPackageMode.Suggestions, response.Mode);
        Assert.Equal(request.CorrelationId, response.CorrelationId);
        Assert.Equal(request.Input, response.Input);
        var suggestion = Assert.Single(response.Suggestions);
        Assert.Equal("Home One", suggestion.Value);
        Assert.Equal(description, suggestion.DescriptionSource);
        Assert.True(suggestion.IsArgument);
    }

    [Fact]
    public void SuggestionResponseIsBounded()
    {
        var response = RoundTrip(
            CommandPackage.CreateSuggestions(
                "/tp self private ",
                "bounded",
                Enumerable.Range(0, 100).Select(index => new CommandSuggestion(index.ToString(), "", true))
            )
        );
        Assert.Equal(64, response.Suggestions.Count);
    }

    [Fact]
    public void RequestRoundTrips()
    {
        var package = CommandPackage.CreateRequest(ResourceId.Parse("game:world/time/get"), [], "request-1");

        var clone = RoundTrip(package);

        Assert.Equal(CommandPackage.CommandPackageMode.Request, clone.Mode);
        Assert.Equal("request-1", clone.CorrelationId);
        Assert.Equal(ResourceId.Parse("game:world/time/get"), clone.CommandId);
        Assert.Empty(clone.Payload);
    }

    [Fact]
    public void CommandsUseReliableControlTransport()
    {
        var transport = PackageTransportPolicy.Get(CommandPackage.CreateRequest(ResourceId.Parse("game:help"), []));

        Assert.Equal(PackageTransportPolicy.Control, transport);
    }

    [Fact]
    public void PermissionSnapshotRoundTrips()
    {
        var playerGuid = Guid.NewGuid();
        var owner = new ModId("game");
        var package = CommandPackage.CreatePermissionSnapshot(
            playerGuid,
            [
                new CommandPermissionGrant(
                    new ResourceId(owner, "server.stop"),
                    false
                ),
                new CommandPermissionGrant(
                    new ResourceId(owner, "world.time.set"),
                    true
                )
            ]
        );

        var clone = RoundTrip(package);

        Assert.Equal(CommandPackage.CommandPackageMode.PermissionSnapshot, clone.Mode);
        Assert.Equal(playerGuid, clone.PlayerGuid);
        Assert.Equal(
            [
                new CommandPermissionGrant(
                    new ResourceId(owner, "server.stop"),
                    false
                ),
                new CommandPermissionGrant(
                    new ResourceId(owner, "world.time.set"),
                    true
                )
            ],
            clone.PermissionGrants
        );
    }

    [Fact]
    public void RequestRoundTripsCommandIdentityAndPayload()
    {
        var commandId = new ResourceId(new ModId("example.commands"), "world/time/set");
        var package = CommandPackage.CreateRequest(
            commandId,
            [1, 2, 3, 4],
            "request-typed"
        );

        var clone = RoundTrip(package);

        Assert.Equal(CommandPackage.CommandPackageMode.Request, clone.Mode);
        Assert.Equal(commandId, clone.CommandId);
        Assert.Equal([1, 2, 3, 4], clone.Payload);
        Assert.Equal("request-typed", clone.CorrelationId);
    }

    [Fact]
    public void ClientFeedbackRoundTripsWithoutServerPublishingPolicy()
    {
        var result = new CommandResult(
            true,
            "team.invitation_pending",
            "等待对方确认。",
            true,
            CommandResultAudience.AllPlayers,
            CommandResultState.Pending,
            CommandResultPresentation.History | CommandResultPresentation.Toast,
            "TeamInvitationPending_Message",
            ["Alice"],
            PanelBehavior: CommandPanelBehavior.CloseOnSuccess
        );
        var package = CommandPackage.CreateResult(CommandResponseBuilder.Capture(result), "request-result");

        var clone = RoundTrip(package);

        Assert.Equal(CommandPackage.CommandPackageMode.Result, clone.Mode);
        Assert.Equal("request-result", clone.CorrelationId);
        Assert.NotNull(clone.Result);
        var restored = CommandResponseBuilder.Restore(clone.Result!);
        Assert.Equal(result.Success, restored.Success);
        Assert.Equal(result.Code, restored.Code);
        Assert.Equal(result.Message, restored.Message);
        Assert.False(restored.Sensitive);
        Assert.Equal(CommandResultAudience.Requester, restored.Audience);
        Assert.Equal(result.State, restored.State);
        Assert.Equal(result.Presentation, restored.Presentation);
        Assert.Equal(result.PanelBehavior, restored.PanelBehavior);
        Assert.Equal(result.MessageKey, restored.MessageKey);
        Assert.Equal(result.MessageArguments, restored.MessageArguments);
    }

    private static CommandPackage RoundTrip(CommandPackage package)
    {
        var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(package, writer);
        using var reader = new ProtocolReader(writer.Data());
        var clone = Assert.IsType<CommandPackage>(ProtocolMessageCodec.Read(package.ID, reader));
        return clone;
    }
}
