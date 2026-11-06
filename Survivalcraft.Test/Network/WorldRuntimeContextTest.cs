using EntitySystem.Core;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.World;

namespace Survivalcraft.Test.Network;

public sealed class WorldRuntimeContextTest
{
    [Fact]
    public void AProjectCannotBelongToBothRuntimeSides()
    {
        using var project = new Project();
        var authority = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Authority);
        var client = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Client);
        authority.BindProject(project);

        Assert.Throws<InvalidOperationException>(() => client.BindProject(project));
        Assert.Same(authority, WorldRuntimeContext.For(project));
        Assert.Null(client.Project);
    }

    [Fact]
    public void ProjectDisposalClosesItsRuntimeAndRemovesItsBinding()
    {
        var project = new Project();
        var runtime = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Client);
        runtime.BindProject(project);

        project.Dispose();

        Assert.True(runtime.IsClosed);
        Assert.Null(runtime.Project);
        Assert.Throws<InvalidOperationException>(() => WorldRuntimeContext.For(project));
        Assert.Throws<ObjectDisposedException>(() =>
            Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Authority).BindProject(project)
        );
    }

    [Fact]
    public void AClosedRuntimeKeepsItsSideAvailableDuringWorldResourceDisposal()
    {
        var project = new DisposalProject();
        var runtime = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Authority);
        runtime.BindProject(project);
        WorldRuntimeContext? duringDisposal = null;
        project.OnResourceDisposal = () => duringDisposal = WorldRuntimeContext.For(project);

        runtime.Close();
        project.Dispose();

        Assert.Same(runtime, duringDisposal);
        Assert.True(duringDisposal!.IsClosed);
        Assert.Throws<InvalidOperationException>(() => WorldRuntimeContext.For(project));
    }

    [Fact]
    public void HandlerStateIsCreatedOncePerRuntimeRatherThanSharedBetweenSides()
    {
        using var authorityProject = new Project();
        using var clientProject = new Project();
        var created = new List<Handler>();
        var builder = new PackageHandlerCatalogBuilder();
        builder.RegisterLogical(() =>
            {
                var handler = new Handler();
                created.Add(handler);
                return handler;
            }
        );
        var catalog = builder.Build();
        var authority = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Authority, catalog);
        var client = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Client, catalog);
        authority.BindProject(authorityProject);
        client.BindProject(clientProject);
        WorldNetworkBinding.For(authority).Dispatch(
            new TestMessage(),
            new PackageReceiveContext(null, authority, null)
        );
        WorldNetworkBinding.For(client).Dispatch(new TestMessage(), new PackageReceiveContext(null, client, null));
        WorldNetworkBinding.For(authority).Dispatch(
            new TestMessage(),
            new PackageReceiveContext(null, authority, null)
        );
        Assert.Equal(2, created.Count);
        Assert.Equal(new[] { authorityProject, authorityProject }, created[0].Worlds);
        Assert.Equal(new[] { clientProject }, created[1].Worlds);
    }

    [Fact]
    public void WorldCatalogCannotBeReplacedAndTheNextWorldGetsFreshHandlerState()
    {
        var first = new Handler();
        var replacement = new Handler();
        var firstBuilder = new PackageHandlerCatalogBuilder();
        firstBuilder.RegisterLogical(() => first);
        var nextBuilder = new PackageHandlerCatalogBuilder();
        nextBuilder.RegisterLogical(() => replacement);
        var nextCatalog = nextBuilder.Build();
        var runtime =
            Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Client, firstBuilder.Build());
        var binding = WorldNetworkBinding.For(runtime);
        var context = new PackageReceiveContext(null, runtime, null);
        binding.Dispatch(new TestMessage(), context);
        Assert.Throws<InvalidOperationException>(() => binding.InstallHandlers(nextCatalog));
        runtime.Close();
        Assert.False(binding.Dispatch(new TestMessage(), context));
        var next = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Client, nextCatalog);
        WorldNetworkBinding.For(next).Dispatch(new TestMessage(), new PackageReceiveContext(null, next, null));
        Assert.Single(first.Worlds);
        Assert.Single(replacement.Worlds);
        next.Close();
    }

    [Fact]
    public void TwoSidesBindIndependentWorldsWithoutChangingTheGlobalWorld()
    {
        using var authorityProject = new Project();
        using var clientProject = new Project();
        var authority = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Authority);
        var client = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Client);
        authority.BindProject(authorityProject);
        client.BindProject(clientProject);

        Assert.NotEqual(authority.Id, client.Id);
        Assert.Same(authorityProject, new PackageReceiveContext(null, authority, null).Project);
        Assert.Same(clientProject, new PackageReceiveContext(null, client, null).Project);
        Assert.True(new PackageReceiveContext(null, authority, null).IsServer);
        Assert.False(new PackageReceiveContext(null, client, null).IsServer);
        Assert.Throws<InvalidOperationException>(() => authority.BindProject(clientProject));
    }

    [Fact]
    public void ClosingAWorldInvalidatesAlreadyQueuedMessagesWithoutAffectingTheOtherSide()
    {
        using var authorityProject = new Project();
        using var clientProject = new Project();
        var handler = new Handler();
        var builder = new PackageHandlerCatalogBuilder();
        builder.RegisterLogical(() => handler);
        var catalog = builder.Build();
        var authority = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Authority, catalog);
        var client = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Client, catalog);
        authority.BindProject(authorityProject);
        client.BindProject(clientProject);
        var queued = new[]
        {
            new ReceivedPackage(new TestMessage(), new PackageReceiveContext(null, authority, null)),
            new ReceivedPackage(new TestMessage(), new PackageReceiveContext(null, client, null))
        };
        authority.Close();
        foreach (var received in queued)
        {
            WorldNetworkBinding.For(received.Context.Runtime).Dispatch(received);
        }

        Assert.Same(clientProject, Assert.Single(handler.Worlds));
        Assert.Null(authority.Project);
        Assert.Throws<ObjectDisposedException>(() => authority.BindProject(authorityProject));
    }

    [Fact]
    public void ReplacingTheNodeRoleDoesNotRetargetExistingReceiveContexts()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        node.Runtime.BindProject(project);
        var queued = node.CreateReceiveContext();

        node.Role = NodeRole.Client;

        Assert.True(queued.Runtime.IsClosed);
        Assert.True(queued.IsServer);
        Assert.Null(queued.Project);
        Assert.NotSame(queued.Runtime, node.Runtime);
        Assert.False(node.CreateReceiveContext().IsServer);
    }

    [Fact]
    public void ReleasingTheWorldInvalidatesItsMessagesBeforeTheNextWorldIsBound()
    {
        using var first = new Project();
        using var next = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        node.TurnOnPackageHandle(first);
        var queued = node.CreateReceiveContext();

        node.ReleaseProject(first);
        node.TurnOnPackageHandle(next);

        Assert.True(queued.Runtime.IsClosed);
        Assert.Null(queued.Project);
        Assert.Same(next, node.CreateReceiveContext().Project);
    }

    private sealed record TestMessage : IProtocolMessage;

    private sealed class DisposalProject : Project
    {
        public DisposalProject()
        {
            subsystems.Add(new Resource(this));
        }

        public Action? OnResourceDisposal { get; set; }

        private sealed class Resource(DisposalProject project) : Subsystem
        {
            public override void Dispose()
            {
                project.OnResourceDisposal?.Invoke();
            }
        }
    }

    private sealed class Handler : PackageHandlerBase<TestMessage>
    {
        public List<Project?> Worlds { get; } = [];

        public override void Handle(TestMessage package, PackageReceiveContext context)
        {
            Worlds.Add(context.Project);
        }
    }
}
