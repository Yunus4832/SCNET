using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

using Engine.Core.Runtime;
using Engine.Core.Security;

using EntitySystem.Core;

using Game.Characters;
using Game.Commands;
using Game.Network;
using Game.Protocol.Identity;
using Game.Subsystems;

using MessagePack;

namespace Survivalcraft.Test.Commands;

public class HttpPlayerCommandAdapterTest
{
    private static readonly ModId _owner = new("test.playerhttp");

    private static readonly ResourceId _permission = new(_owner, "world.change");

    private static readonly ResourceId _identity = new(_owner, "world/change");

    [Fact]
    public void CatalogIsAnOrdinaryCommandAndContainsOnlyAuthorizedCommandIdentities()
    {
        var previous = CommonLib.WorkType;
        try
        {
            CommonLib.WorkType = WorkType.Server;
            using var project = new TestProject();
            var player = Player(project);
            var registry = Registry();
            var context = new CommandContext(
                CommandInvocationChannel.Network,
                CommandPrincipal.FromSubject(
                    player.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.GetOrCreate(player.OwnerAddress),
                    player
                ),
                null,
                "catalog"
            );
            Assert.True(HttpPlayerCommandAdapter.TryCreateCommand(registry, null, out var command, out _));
            Assert.IsType<GetCommandCatalogCommand>(command);
            Assert.True(CommandGateway.TryCreateRequest(registry, command!, "catalog", out var package, out _));
            var received = RoundTrip(package!);
            Assert.True(registry.TryDecode(received.CommandId, received.Payload, out var decoded, out _));
            var dispatcher = new CommandDispatcher(registry);
            var result = dispatcher.Execute(decoded!, context);
            Assert.True(result.Success);
            var ids = result.Data!["commands"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray();
            Assert.DoesNotContain(_identity.ToString(), ids);
            Assert.DoesNotContain("test.playerhttp:application/change", ids);
            player.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.GetOrCreate(player.OwnerAddress).Permissions
                .Grant(_permission, false);
            context = new CommandContext(
                CommandInvocationChannel.Network,
                CommandPrincipal.FromSubject(
                    player.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.GetOrCreate(player.OwnerAddress),
                    player
                ),
                null,
                "catalog"
            );
            result = dispatcher.Execute(decoded!, context);
            Assert.Contains(
                _identity.ToString(),
                result.Data!["commands"]!.AsArray()
                    .Select(value => value!.GetValue<string>())
            );
        }
        finally
        {
            CommonLib.WorkType = previous;
        }
    }

    [Fact]
    public void EveryBuiltInWorldAndServerCommandHasABinaryCodec()
    {
        var registry = new CommandRegistry();
        BuiltInCommands.Register(registry, new ModId("game"));
        Assert.All(
            registry.Definitions.Where(entry => entry.Definition.Domain is not CommandDomain.Application),
            entry => Assert.True(entry.Definition.SupportsRemoteInvocation, entry.Id.ToString())
        );
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
        await cancellation.CancelAsync();
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
    public void HttpAndTextAdaptersProduceTheSameBinaryCommandWithoutExecutingIt()
    {
        var registry = new CommandRegistry();
        BuiltInCommands.Register(registry, new ModId("game"));
        var http = new HttpCommandAdapter(registry);
        var context = new CommandContext(CommandInvocationChannel.Text, CommandPrincipal.LocalOperator, null, "same");
        Assert.True(
            http.TryCreate(
                new HttpCommandRequest(
                    ResourceId.Parse("game:world/time/set"),
                    new JsonObject { ["preset"] = "night" }
                ),
                out var httpCommand,
                out _
            )
        );
        Assert.True(new TextCommandAdapter(registry).TryCreate("/time set night", context, out var textCommand, out _));
        Assert.Equal(new SetWorldTimeCommand("night"), httpCommand);
        Assert.Equal(textCommand, httpCommand);
        Assert.True(CommandGateway.TryCreateRequest(registry, httpCommand!, "same", out var httpPackage, out _));
        Assert.True(CommandGateway.TryCreateRequest(registry, textCommand!, "same", out var textPackage, out _));
        Assert.Equal(CommandPackage.CommandPackageMode.Request, httpPackage!.Mode);
        Assert.Equal(httpPackage.CommandId, textPackage!.CommandId);
        Assert.Equal(httpPackage.Payload, textPackage.Payload);
        var decodedPackage = RoundTrip(httpPackage);
        Assert.True(registry.TryDecode(decodedPackage.CommandId, decodedPackage.Payload, out var decoded, out _));
        Assert.Equal(httpCommand, decoded);
    }

    [Theory]
    [InlineData("a", 4092, true)]
    [InlineData("a", 4093, false)]
    [InlineData("a", 5000, false)]
    [InlineData("中", 1364, true)]
    [InlineData("中", 1365, false)]
    public void RemoteRequestsRejectOversizedEncodedArgumentsBeforeQueueing(string unit, int count, bool accepted)
    {
        var registry = new CommandRegistry();
        BuiltInCommands.Register(registry, new ModId("game"));
        var request = new HttpCommandRequest(
            ResourceId.Parse("game:world/time/set"),
            new JsonObject { ["preset"] = string.Concat(Enumerable.Repeat(unit, count)) }
        );
        Assert.True(new HttpCommandAdapter(registry).TryCreate(request, out var command, out _));
        Assert.Equal(
            accepted,
            CommandGateway.TryCreateRequest(registry, command!, "bounded", out var package, out var error)
        );
        if (accepted)
        {
            Assert.Null(error);
            Assert.True(package!.Payload.Length <= ProtocolLimits.MaximumCommandPayloadLength);
            Assert.Equal(package.Payload, RoundTrip(package).Payload);
        }
        else
        {
            Assert.Null(package);
            Assert.Equal("command.too_long", error!.Code);
        }
    }

    [Fact]
    public void BindingBeforeTransportDoesNotGrantServerPermission()
    {
        var previous = CommonLib.WorkType;
        try
        {
            CommonLib.WorkType = WorkType.Server;
            using var project = new TestProject();
            var player = Player(project);
            var registry = Registry();
            Assert.True(
                HttpPlayerCommandAdapter.TryCreateCommand(
                    registry,
                    new HttpCommandRequest(_identity, new JsonObject()),
                    out var command,
                    out _
                )
            );
            Assert.True(CommandGateway.TryCreateRequest(registry, command!, "request", out var package, out _));
            var received = RoundTrip(package!);
            Assert.True(registry.TryDecode(received.CommandId, received.Payload, out var decoded, out _));
            var dispatcher = new CommandDispatcher(registry);
            var context = new CommandContext(
                CommandInvocationChannel.Network,
                CommandPrincipal.FromSubject(
                    player.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.GetOrCreate(player.OwnerAddress),
                    player
                ),
                project,
                "request"
            );
            Assert.True(
                new TextCommandAdapter(registry).TryCreate(
                    "/change",
                    context,
                    out var textCommand,
                    out _
                )
            );
            Assert.Equal(command, textCommand);
            Assert.Equal("command.forbidden", dispatcher.Execute(decoded!, context).Code);
            player.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.GetOrCreate(player.OwnerAddress).Permissions
                .Grant(_permission, false);
            context = new CommandContext(
                CommandInvocationChannel.Network,
                CommandPrincipal.FromSubject(
                    player.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.GetOrCreate(player.OwnerAddress),
                    player
                ),
                project,
                "request"
            );
            Assert.True(dispatcher.Execute(decoded!, context).Success);
            Assert.False(
                HttpPlayerCommandAdapter.TryCreateCommand(
                    registry,
                    new HttpCommandRequest(new ResourceId(_owner, "application/change"), new JsonObject()),
                    out _,
                    out var error
                )
            );
            Assert.Equal("command.player_scope", error!.Code);
        }
        finally
        {
            CommonLib.WorkType = previous;
        }
    }

    private static T RoundTrip<T>(T package) where T : CommandPackage
    {
        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var result = Assert.IsType<T>(ProtocolMessageCodec.Decode(reader));
        return result;
    }

    private static CommandRegistry Registry()
    {
        var registry = new CommandRegistry();
        BuiltInCommands.Register(registry, new ModId("game"));
        registry.Permissions.Register(_owner, _permission, new CommandPermissionDefinition(CommandDomain.World));
        registry.Register(
            _owner,
            _identity,
            new CommandDefinition<WorldCommand>(
                (_, _) => CommandResult.Ok("changed"),
                CommandDomain.World,
                requiredPermission: _permission,
                supportsRemoteInvocation: true
            )
        );
        registry.Adapters.Register(_owner, _identity, HttpCommandBinding.Create(_ => new WorldCommand()));
        var application = new ResourceId(_owner, "application/change");
        registry.Register(
            _owner,
            application,
            new CommandDefinition<ApplicationCommand>(
                (_, _) => CommandResult.Ok("application"),
                CommandDomain.Application
            )
        );
        registry.Adapters.Register(_owner, application, HttpCommandBinding.Create(_ => new ApplicationCommand()));
        registry.Adapters.Register(
            _owner,
            _identity,
            new TextCommand(
                "change",
                LocalizedText.Empty,
                [new CommandRoute([], typeof(WorldCommand), _ => new WorldCommand())]
            )
        );
        registry.Freeze();
        return registry;
    }

    private static CharacterData Player(Project project)
    {
        var player = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        player.Name = "Test player";
        typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(player, project);
        player.SubsystemCharacters = new SubsystemCharacters();
        player.OwnerAddress = new PlayerAddress(IdentityAddress.Encode(new byte[32]));

        return player;
    }

    private sealed class TestProject : Project
    {
        public TestProject() => subsystems.Add(new SubsystemPlayers());
    }

    [MessagePackObject(AllowPrivate = true)]
    internal sealed record WorldCommand : IGameCommand;

    private sealed record ApplicationCommand : IGameCommand;
}
