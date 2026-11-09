using System.Reflection;

using EntitySystem.Core;

using Game.Managers;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class GameRuntimeTest
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void HostEntryPumpsMessagesBeforeUpdatingEachOwnedWorldOnce(
        bool hasAuthority,
        bool hasClient,
        bool allowsPause
    )
    {
        using var gameRuntime = new GameRuntime(allowsPause);
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var calls = new List<string>();
        if (hasAuthority)
        {
            gameRuntime.Attach(WorldRuntimeContext.For(authority));
            AddUpdate(authority, () => calls.Add("authority"));
        }

        if (hasClient)
        {
            var runtime = WorldRuntimeContext.For(client);
            gameRuntime.Attach(runtime);
            AddUpdate(client, () => calls.Add("client"));
            WorldNetworkBinding.For(runtime).RequireNode().OnReceive += (_, _) => calls.Add("message");
            Assert.True(
                WorldNetworkBinding.For(runtime).Inbox.TryPostOwned(
                    new ReceivedPackage(
                        new SubsystemTimePackage(1, 0),
                        WorldNetworkBinding.For(runtime).RequireNode().CreateReceiveContext()
                    )
                )
            );
        }

        var controller =
            typeof(GameManager).GetField("_worlds", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var runtimeProperty = typeof(GameWorldController).GetProperty(nameof(GameWorldController.Runtime))!;
        var autosaveField = typeof(GameManager).GetField(
            "_lastAutosaveTime",
            BindingFlags.Static | BindingFlags.NonPublic
        )!;
        var previousRuntime = GameManager.Runtime;
        var previousAutosave = autosaveField.GetValue(null);
        try
        {
            runtimeProperty.SetValue(controller, gameRuntime);
            autosaveField.SetValue(null, Engine.Core.Runtime.Time.RealTime);
            GameManager.UpdateRuntime();
            var expected = new List<string>();
            if (hasClient) { expected.Add("message"); }

            if (hasAuthority) { expected.Add("authority"); }

            if (hasClient) { expected.Add("client"); }

            Assert.Equal(expected, calls);
            calls.Clear();
            GameManager.UpdateRuntime();
            Assert.Equal(expected.Where(call => call != "message"), calls);
        }
        finally
        {
            runtimeProperty.SetValue(controller, previousRuntime);
            autosaveField.SetValue(null, previousAutosave);
        }
    }

    [Fact]
    public void LocalAdministrationIsAvailableOnlyToTheRuntimesLivePresentationProject()
    {
        using var gameRuntime = new GameRuntime(false);
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        using var unrelated = new RuntimeTestProject(RuntimeSide.Client);
        var owner = WorldRuntimeContext.For(authority);
        gameRuntime.Attach(owner);
        gameRuntime.Attach(WorldRuntimeContext.For(client));
        Assert.Same(owner, gameRuntime.GetAuthorityForClient(client));
        Assert.Null(gameRuntime.GetAuthorityForClient(unrelated));
        Assert.Null(gameRuntime.GetAuthorityForClient(authority));
        WorldRuntimeContext.For(client).Close();
        Assert.Null(gameRuntime.GetAuthorityForClient(client));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LocalPauseStopsBothWorldsOnlyWhenAllowedAndKeepsTheMessagePumpAlive(bool allowed)
    {
        using var gameRuntime = new GameRuntime(allowed);
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var calls = new List<string>();
        AddUpdate(authority, () => calls.Add("authority"));
        AddUpdate(client, () => calls.Add("client"));
        var authorityRuntime = WorldRuntimeContext.For(authority);
        var clientRuntime = WorldRuntimeContext.For(client);
        gameRuntime.Attach(authorityRuntime);
        gameRuntime.Attach(clientRuntime);
        var received = 0;
        WorldNetworkBinding.For(clientRuntime).RequireNode().OnReceive +=
            (_, messages) => received += messages.Count();
        WorldNetworkBinding.For(clientRuntime).Inbox.TryPostOwned(
            new ReceivedPackage(
                new SubsystemTimePackage(1, 0),
                WorldNetworkBinding.For(clientRuntime).RequireNode().CreateReceiveContext()
            )
        );

        gameRuntime.SetLocalPause(true);
        gameRuntime.UpdateWorlds();
        gameRuntime.UpdateMessages();

        Assert.Equal(allowed, gameRuntime.IsPaused);
        Assert.Equal(allowed ? 0 : 2, calls.Count);
        Assert.Equal(1, received);
        Assert.Equal(allowed ? 0f : 1f, authority.FindSubsystem<SubsystemTime>(true)!.GameTimeFactor);
        Assert.Equal(allowed ? 0f : 1f, client.FindSubsystem<SubsystemTime>(true)!.GameTimeFactor);
        gameRuntime.SetLocalPause(false);
        gameRuntime.UpdateWorlds();
        Assert.Equal(allowed ? 2 : 4, calls.Count);
        Assert.Equal(1f, authority.FindSubsystem<SubsystemTime>(true)!.GameTimeFactor);
    }

    [Fact]
    public void LeavingAPausedLocalClientResumesTheRetainedAuthority()
    {
        using var gameRuntime = new GameRuntime(true);
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var calls = 0;
        AddUpdate(authority, () => calls++);
        AddUpdate(client, () => { });
        gameRuntime.Attach(WorldRuntimeContext.For(authority));
        gameRuntime.Attach(WorldRuntimeContext.For(client));
        gameRuntime.SetLocalPause(true);
        Assert.True(gameRuntime.IsPaused);

        gameRuntime.ReleaseClient();
        gameRuntime.UpdateWorlds();

        Assert.False(gameRuntime.IsPaused);
        Assert.Equal(1, calls);
        Assert.Equal(1f, authority.FindSubsystem<SubsystemTime>(true)!.GameTimeFactor);
    }

    [Fact]
    public void ReplacingAClientWorldKeepsTheAuthorityAlive()
    {
        using var gameRuntime = new GameRuntime(false);
        var authority = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var client = new WorldRuntimeContext(
            RuntimeSide.Client,
            true
        );
        var authorityProject = new Project();
        var clientProject = new Project();
        authority.BindProject(authorityProject);
        client.BindProject(clientProject);
        gameRuntime.Attach(authority);
        gameRuntime.Attach(client);

        gameRuntime.ReleaseClient();

        Assert.True(clientProject.IsDisposed);
        Assert.True(client.IsClosed);
        Assert.False(authorityProject.IsDisposed);
        Assert.Same(authorityProject, gameRuntime.Authority!.Project);
        Assert.Null(gameRuntime.Client);
        var replacement = new WorldRuntimeContext(
            RuntimeSide.Client,
            true
        );
        gameRuntime.Attach(replacement);
        gameRuntime.Dispose();
        Assert.True(authorityProject.IsDisposed);
        Assert.True(replacement.IsClosed);
    }

    [Fact]
    public void ClosingTheClientStillReleasesAuthorityIfCancellationFails()
    {
        var gameRuntime = new GameRuntime(false);
        var authority = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var client = new WorldRuntimeContext(
            RuntimeSide.Client,
            true
        );
        var authorityProject = new Project();
        var clientProject = new Project();
        authority.BindProject(authorityProject);
        client.BindProject(clientProject);
        gameRuntime.Attach(authority);
        gameRuntime.Attach(client);
        using var registration = client.Lifetime.Register(() => throw new InvalidOperationException("probe"));

        Assert.Throws<AggregateException>(gameRuntime.Dispose);

        Assert.True(clientProject.IsDisposed);
        Assert.True(authorityProject.IsDisposed);
        Assert.True(authority.IsClosed);
        Assert.True(gameRuntime.IsDisposed);
        gameRuntime.Dispose();
    }

    [Fact]
    public void BothSidesCannotShareAMessageNode()
    {
        using var gameRuntime = new GameRuntime(false);
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        gameRuntime.Attach(node.Runtime);
        var client = new WorldRuntimeContext(RuntimeSide.Client, true);
        _ = new WorldNetworkBinding(client, node, PackageHandlerCatalog.Empty);
        Assert.Throws<InvalidOperationException>(() => gameRuntime.Attach(client));
        client.Close();
    }

    [Fact]
    public void BothProjectsAdvanceInAuthorityThenClientOrder()
    {
        using var gameRuntime = new GameRuntime(false);
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var calls = new List<string>();
        AddUpdate(authority, () => calls.Add("authority"));
        AddUpdate(client, () => calls.Add("client"));
        gameRuntime.Attach(WorldRuntimeContext.For(authority));
        gameRuntime.Attach(WorldRuntimeContext.For(client));

        gameRuntime.UpdateWorlds();

        Assert.Equal(new[] { "authority", "client" }, calls);
        gameRuntime.ReleaseClient();
        gameRuntime.UpdateWorlds();
        Assert.Equal(new[] { "authority", "client", "authority" }, calls);
    }

    [Fact]
    public void TransportShutdownDoesNotLoseTheProjectsThatStillNeedDisposal()
    {
        using var gameRuntime = new GameRuntime(false);
        var authority = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var client = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        var authorityProject = new Project();
        var clientProject = new Project();
        authority.Runtime.BindProject(authorityProject);
        client.Runtime.BindProject(clientProject);
        gameRuntime.Attach(authority.Runtime);
        gameRuntime.Attach(client.Runtime);

        authority.StopImmediate();
        client.StopImmediate();
        var nextAuthority = authority.Runtime;
        var nextClient = client.Runtime;
        Assert.Null(gameRuntime.Authority!.Project);
        Assert.Null(gameRuntime.Client!.Project);
        gameRuntime.Dispose();

        Assert.True(authorityProject.IsDisposed);
        Assert.True(clientProject.IsDisposed);
        Assert.Same(nextAuthority, authority.Runtime);
        Assert.Same(nextClient, client.Runtime);
    }

    [Theory]
    [InlineData(RuntimeSide.Authority)]
    [InlineData(RuntimeSide.Client)]
    public void ReleasingAnAlreadyDisposedProjectStillStopsItsOwnedEndpoint(RuntimeSide side)
    {
        using var gameRuntime = new GameRuntime(false);
        using var project = new RuntimeTestProject(side);
        var world = WorldRuntimeContext.For(project);
        var node = WorldNetworkBinding.For(world).RequireNode();
        node.OnReceive += (_, _) => { };
        gameRuntime.Attach(world);

        project.Dispose();
        Assert.False(WorldNetworkBinding.TryGet(world, out _));

        if (side == RuntimeSide.Client) { gameRuntime.ReleaseClient(); }
        else { gameRuntime.Dispose(); }

        Assert.Equal(NodeRole.Inactive, node.Role);
        Assert.NotSame(world, node.Runtime);
        node.Update();
    }

    private static void AddUpdate(RuntimeTestProject project, Action action)
    {
        var update = new SubsystemUpdate { UpdatesPerFrame = 1 };
        var time = new SubsystemTime();
        project.AddSubsystem(update);
        project.AddSubsystem(time);
        project.AddSubsystem(new SubsystemCharacters());
        update.Load(new EntitySystem.TemplatesDatabase.ValuesDictionary());
        var pending = (Dictionary<IUpdateable, bool>)typeof(SubsystemUpdate)
            .GetField("_toAddOrRemove", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(update)!;
        pending.Add(new UpdateProbe(action), true);
    }

    private sealed class UpdateProbe(Action action) : IUpdateable
    {
        public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public void Update(float dt) => action();
    }
}
