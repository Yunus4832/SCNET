using System.Reflection;

using Game.Application;
using Game.Managers;
using Game.Network;
using Game.Network.Handling;
using Game.World;
using Game.World.Presentation;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Managers;

[Collection(ConfigFileCollection.Name)]
public sealed class LocalPlayerLeaveTest
{
    [Fact]
    public void LeavingLocalPlayerClearsClientPresentationAndKeepsAuthorityAlive()
    {
        var controller = (GameWorldController)typeof(GameManager)
            .GetField("_worlds", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var runtimeProperty = typeof(GameWorldController).GetProperty(nameof(GameWorldController.Runtime))!;
        var nodeField = typeof(CommonLib).GetField("_net", BindingFlags.Static | BindingFlags.NonPublic)!;
        var presentationField = typeof(ClientApplication).GetField(
            "_world",
            BindingFlags.Static | BindingFlags.NonPublic
        )!;
        var previousRuntime = controller.Runtime;
        var previousNode = nodeField.GetValue(null);
        var previousPresentation = presentationField.GetValue(null);
        using var game = new GameRuntime(false);
        var authority = new NetNode(NetNodeOptions.Default, false, PackageHandlerCatalog.Empty, NodeRole.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var clientRuntime = WorldRuntimeContext.For(client);
        game.Attach(authority.Runtime);
        game.Attach(clientRuntime);
        var presentation = new ClientWorldPresentation(clientRuntime, client, null, authority.Runtime);
        try
        {
            runtimeProperty.SetValue(controller, game);
            nodeField.SetValue(null, authority);
            presentationField.SetValue(null, presentation);

            GameManager.LeaveLocalPlayer();

            Assert.Null(ClientApplication.World);
            Assert.Null(game.Client);
            Assert.True(client.IsDisposed);
            Assert.Same(authority.Runtime, game.Authority);
            Assert.False(authority.Runtime.IsClosed);
        }
        finally
        {
            presentation.Dispose();
            runtimeProperty.SetValue(controller, previousRuntime);
            nodeField.SetValue(null, previousNode);
            presentationField.SetValue(null, previousPresentation);
        }
    }
}
