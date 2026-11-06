using System.Reflection;

using Game.Managers;
using Game.Network;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Managers;

[Collection(ConfigFileCollection.Name)]
public sealed class WorldReplacementOwnershipTest
{
    [Fact]
    public void LoadingAnotherWorldCannotDisposeAnExistingAuthorityWithoutListeningPorts()
    {
        var controller = (GameWorldController)typeof(GameManager)
            .GetField("_worlds", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var runtimeProperty = typeof(GameWorldController).GetProperty(nameof(GameWorldController.Runtime))!;
        var previousRuntime = GameManager.Runtime;
        var previousRole = CommonLib.Net.Role;
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        using var session = new GameRuntime(false);
        session.Attach(WorldRuntimeContext.For(project));
        try
        {
            CommonLib.Net.Role = NodeRole.Authority;
            runtimeProperty.SetValue(controller, session);
            Assert.False(CommonLib.Net.IsTransportRunning);

            Assert.Throws<InvalidOperationException>(() => controller.LoadAuthority(null!));

            Assert.Same(session, GameManager.Runtime);
            Assert.Same(project, session.Authority!.BoundProject);
            Assert.False(project.IsDisposed);
            Assert.False(session.Authority.IsClosed);
        }
        finally
        {
            runtimeProperty.SetValue(controller, previousRuntime);
            CommonLib.Net.Role = previousRole;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreparingAnAuthorityCannotResetSessionsWhileItsProjectIsStillOwned(bool closed)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var node = WorldNetworkBinding.For(project).Node!;
        var player = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        node.AddClient(player);
        if (closed) { node.Runtime.Close(); }

        Assert.Throws<InvalidOperationException>(node.PrepareAuthorityWorld);

        Assert.Same(player, Assert.Single(node.Clients).Value);
        Assert.False(project.IsDisposed);
        project.Dispose();
        node.PrepareAuthorityWorld();
        Assert.Empty(node.Clients);
    }
}
