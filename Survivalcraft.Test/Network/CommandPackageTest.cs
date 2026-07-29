using Game.Commands;
using Game.Modding;
using Game.Network;
using Game.Network.Packages;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Network;

public class CommandPackageTest
{
    [Fact]
    public void RequestRoundTrips()
    {
        var package = CommandPackage.CreateRequest("/time get", "request-1");

        var clone = RoundTrip(package);

        Assert.Equal(CommandPackage.CommandPackageMode.Request, clone.Mode);
        Assert.Equal("request-1", clone.CorrelationId);
        Assert.Equal("/time get", clone.Input);
    }

    [Fact]
    public void CommandsUseReliableControlTransport()
    {
        var transport = PackageTransportPolicy.Get(CommandPackage.CreateRequest("/help"));

        Assert.Equal(PackageTransportPolicy.Control, transport);
    }

    [Fact]
    public void PermissionSnapshotRoundTrips()
    {
        var playerGuid = Guid.NewGuid();
        var package = CommandPackage.CreatePermissionSnapshot(
            playerGuid,
            [
                new CommandPermissionGrant("server.stop", false),
                new CommandPermissionGrant("world.*", true)
            ]);

        var clone = RoundTrip(package);

        Assert.Equal(CommandPackage.CommandPackageMode.PermissionSnapshot, clone.Mode);
        Assert.Equal(playerGuid, clone.PlayerGuid);
        Assert.Equal(
            [
                new CommandPermissionGrant("server.stop", false),
                new CommandPermissionGrant("world.*", true)
            ],
            clone.PermissionGrants);
    }

    [Fact]
    public void TypedRequestRoundTripsCommandIdentityAndPayload()
    {
        var commandId = new ResourceId(new ModId("example.commands"), "world/time/set");
        var package = CommandPackage.CreateRequest(
            commandId,
            [1, 2, 3, 4],
            "request-typed");

        var clone = RoundTrip(package);

        Assert.Equal(CommandPackage.CommandPackageMode.TypedRequest, clone.Mode);
        Assert.Equal(commandId, clone.CommandId);
        Assert.Equal([1, 2, 3, 4], clone.Payload);
        Assert.Equal("request-typed", clone.CorrelationId);
    }

    [Fact]
    public void StructuredResultRoundTrips()
    {
        var result = new CommandResult(
            true,
            "team.invitation_pending",
            "等待对方确认。",
            true,
            CommandResultAudience.AllPlayers,
            CommandResultState.Pending,
            CommandResultPresentation.Silent,
            "TeamInvitationPending_Message",
            ["Alice"]);
        var package = CommandPackage.CreateResult(result, "request-result");

        var clone = RoundTrip(package);

        Assert.Equal(CommandPackage.CommandPackageMode.Result, clone.Mode);
        Assert.Equal("request-result", clone.CorrelationId);
        Assert.NotNull(clone.Result);
        Assert.Equal(result.Success, clone.Result!.Success);
        Assert.Equal(result.Code, clone.Result.Code);
        Assert.Equal(result.Message, clone.Result.Message);
        Assert.Equal(result.Sensitive, clone.Result.Sensitive);
        Assert.Equal(result.Audience, clone.Result.Audience);
        Assert.Equal(result.State, clone.Result.State);
        Assert.Equal(result.Presentation, clone.Result.Presentation);
        Assert.Equal(result.MessageKey, clone.Result.MessageKey);
        Assert.Equal(result.MessageArguments, clone.Result.MessageArguments);
    }

    private static CommandPackage RoundTrip(CommandPackage package)
    {
        var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var clone = new CommandPackage();
        clone.ReadData(reader);
        return clone;
    }
}
