using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

using Engine.Core;

using EntitySystem.Core;

using Game;
using Game.Commands;
using Game.Modding;
using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Commands;

public class HttpPlayerCommandAdapterTest
{
    private static readonly ModId _owner = new("test.playerhttp");
    private static readonly ResourceId _permission = new(_owner, "world.change");
    private static readonly ResourceId _identity = new(_owner, "world/change");

    [Fact]
    public void PlayerRequestsRequireTheirOwnPermissionAndExcludeApplicationCommands()
    {
        var previous = CommonLib.WorkType;
        try
        {
            CommonLib.WorkType = WorkType.Server;
            using var project = new TestProject();
            var player = Player(project);
            var registry = Registry();
            var request = new HttpCommandRequest(_identity, new JsonObject());

            Assert.Equal("command.forbidden", HttpPlayerCommandAdapter.Execute(registry, request, player, "denied").Code);
            Assert.Empty(HttpPlayerCommandAdapter.Execute(registry, null, player, "discover").Data!["commands"]!.AsArray());

            player.CommandPermissions.Grant(_permission, false);
            Assert.True(HttpPlayerCommandAdapter.Execute(registry, request, player, "allowed").Success);
            var commands = HttpPlayerCommandAdapter.Execute(registry, null, player, "discover").Data!["commands"]!.AsArray();
            Assert.Equal(_identity.ToString(), Assert.Single(commands)!["identity"]!.GetValue<string>());
            Assert.Equal("command.player_scope", HttpPlayerCommandAdapter.Execute(registry,
                new HttpCommandRequest(new ResourceId(_owner, "application/change"), new JsonObject()), player, "application").Code);
        }
        finally
        {
            CommonLib.WorkType = previous;
        }
    }

    [Theory]
    [InlineData(RunModeType.HeadlessServer)]
    [InlineData(RunModeType.Gui)]
    public async Task NoActivePlayerCannotBecomeLocalOperator(RunModeType runMode)
    {
        var previous = RunMode.Value;
        try
        {
            RunMode.Value = runMode;
            var task = HttpPlayerCommandAdapter.SubmitAsync(null, CancellationToken.None);
            HttpPlayerCommandAdapter.Update();
            Assert.Equal("command.no_player", (await task).Code);
        }
        finally
        {
            RunMode.Value = previous;
            HttpPlayerCommandAdapter.FailPending();
        }
    }

    [Fact]
    public async Task CancelledQueuedRequestDoesNotExecute()
    {
        using var cancellation = new CancellationTokenSource();
        var task = HttpPlayerCommandAdapter.SubmitAsync(null, cancellation.Token);
        cancellation.Cancel();
        HttpPlayerCommandAdapter.Update();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task HostShutdownCompletesPendingPlayerRequests()
    {
        var task = HttpPlayerCommandAdapter.SubmitAsync(null, CancellationToken.None);
        HttpPlayerCommandAdapter.FailPending();
        Assert.Equal("command.unavailable", (await task).Code);
    }

    [Fact]
    public void RemoteHttpEnvelopeAndResultDataRoundTrip()
    {
        var request = new HttpCommandRequest(_identity, new JsonObject { ["enabled"] = true });
        var package = RoundTrip(CommandPackage.CreateHttpRequest(request, "request"));
        Assert.Equal(CommandPackage.CommandPackageMode.HttpRequest, package.Mode);
        Assert.True(HttpCommandProtocol.TryParseEnvelope(JsonNode.Parse(package.Input)!.AsObject(), out var decoded, out _));
        Assert.Equal(_identity, decoded!.Identity);
        Assert.True(decoded.Arguments["enabled"]!.GetValue<bool>());
        Assert.Equal(CommandPackage.CommandPackageMode.HttpDiscoveryRequest,
            RoundTrip(CommandPackage.CreateHttpRequest(null, "discover")).Mode);

        var result = CommandResult.Pending("accepted") with { Data = new JsonObject { ["value"] = 42 } };
        var response = RoundTrip(CommandPackage.CreateHttpResult(result, "result"));
        Assert.Equal(CommandPackage.CommandPackageMode.HttpResult, response.Mode);
        Assert.Equal(CommandResultState.Pending, response.Result!.State);
        Assert.Equal(42, response.Result.Data!["value"]!.GetValue<int>());
    }

    private static CommandPackage RoundTrip(CommandPackage package)
    {
        using var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var result = new CommandPackage();
        result.ReadData(reader);
        return result;
    }

    private static CommandRegistry Registry()
    {
        var registry = new CommandRegistry();
        registry.Permissions.Register(_owner, _permission, new CommandPermissionDefinition(CommandDomain.World));
        registry.Register(_owner, _identity, new CommandDefinition<WorldCommand>(
            (_, _) => CommandResult.Ok("changed"), CommandDomain.World, requiredPermission: _permission));
        registry.Adapters.Register(_owner, _identity, HttpCommandBinding.Create(_ => new WorldCommand()));
        var application = new ResourceId(_owner, "application/change");
        registry.Register(_owner, application, new CommandDefinition<ApplicationCommand>(
            (_, _) => CommandResult.Ok("application"), CommandDomain.Application));
        registry.Adapters.Register(_owner, application, HttpCommandBinding.Create(_ => new ApplicationCommand()));
        registry.Freeze();
        return registry;
    }

    private static PlayerData Player(Project project)
    {
        var player = (PlayerData)RuntimeHelpers.GetUninitializedObject(typeof(PlayerData));
        player.Name = "Test player";
        typeof(PlayerData).GetField("<Project>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(player, project);
        typeof(PlayerData).GetField("<CommandPermissions>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(player, new CommandPermissionSet());
        return player;
    }

    private sealed class TestProject : Project;

    private sealed record WorldCommand : IGameCommand;

    private sealed record ApplicationCommand : IGameCommand;
}
