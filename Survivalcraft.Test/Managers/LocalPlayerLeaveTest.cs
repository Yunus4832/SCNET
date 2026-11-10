using System.Reflection;

using EntitySystem.TemplatesDatabase;

using Game.Application;
using Game.Managers;
using Game.Network;
using Game.Network.Handling;
using Game.Screens;
using Game.Subsystems;
using Game.Widgets;
using Game.World;
using Game.World.Presentation;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Managers;

[Collection(ConfigFileCollection.Name)]
public sealed class LocalPlayerLeaveTest
{
    [Fact]
    public void InitialEntryRequestsBelongToTheWorldAndCompleteBeforeGameplay()
    {
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        using var presentation = new ClientWorldPresentation(WorldRuntimeContext.For(client), client, null, null);
        var requests = 0;
        presentation.EnterRequested += () => requests++;
        Assert.True(presentation.IsEnteringWorld);
        presentation.RequestEnter();
        Assert.Equal(1, requests);
        presentation.CompleteEntry();
        Assert.False(presentation.IsEnteringWorld);
        presentation.Dispose();
        presentation.RequestEnter();
        Assert.Equal(1, requests);
    }

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HostedWorldReturnRetainsOnlyALiveAuthority(bool closeAuthority)
    {
        var controller = (GameWorldController)typeof(GameManager)
            .GetField("_worlds", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var runtimeProperty = typeof(GameWorldController).GetProperty(nameof(GameWorldController.Runtime))!;
        var infoProperty = typeof(GameWorldController).GetProperty(nameof(GameWorldController.WorldInfo))!;
        var sessionProperty = typeof(GameWorldController).GetProperty(nameof(GameWorldController.WorldSession))!;
        var nodeField = typeof(CommonLib).GetField("_net", BindingFlags.Static | BindingFlags.NonPublic)!;
        var presentationField = typeof(ClientApplication).GetField(
            "_world",
            BindingFlags.Static | BindingFlags.NonPublic
        )!;
        var rootField = typeof(ScreensManager).GetField(
            "<RootWidget>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic
        )!;
        var animationField = typeof(ScreensManager).GetField(
            "_animationData",
            BindingFlags.Static | BindingFlags.NonPublic
        )!;
        var previousRuntime = controller.Runtime;
        var previousInfo = controller.WorldInfo;
        var previousSession = controller.WorldSession;
        var previousReturn = controller.LocalPlayerReturnTarget;
        var previousNode = nodeField.GetValue(null);
        var previousPresentation = presentationField.GetValue(null);
        var previousScreens = ScreensManager.Screens;
        var previousRoot = rootField.GetValue(null);
        var previousCurrent = ScreensManager.CurrentScreen;
        var previousPrevious = ScreensManager.PreviousScreen;
        var previousAnimation = animationField.GetValue(null);
        var authority = new NetNode(NetNodeOptions.Default, false, PackageHandlerCatalog.Empty, NodeRole.Authority);
        var authorityWorld = authority.Runtime;
        var scope = new WorldCatalogScope(authority);
        var client = new RuntimeTestProject(RuntimeSide.Client);
        var clientWorld = WorldRuntimeContext.For(client);
        var game = new GameRuntime(false);
        game.Attach(authorityWorld);
        game.Attach(clientWorld);
        scope.Project.AddSubsystem(
            new SubsystemGameInfo
            {
                WorldSettings = new WorldSettings { Name = "hosted-stop-boundary" }, DirectoryName = string.Empty
            }
        );
        var lifecycle = new SubsystemWorldLifecycle();
        scope.Project.AddSubsystem(lifecycle);
        lifecycle.Load(new ValuesDictionary());
        var presentation = new ClientWorldPresentation(clientWorld, client, null, authorityWorld);
        var play = new Screen();
        var hosted = new Screen();
        try
        {
            runtimeProperty.SetValue(controller, game);
            infoProperty.SetValue(controller, null);
            sessionProperty.SetValue(controller, null);
            controller.LocalPlayerReturnTarget = LocalGameReturnTarget.HostedWorld;
            nodeField.SetValue(null, authority);
            presentationField.SetValue(null, presentation);
            ScreensManager.Screens = new Dictionary<string, Screen>();
            ScreensManager.AddScreen("Play", play);
            ScreensManager.AddScreen("HostedWorld", hosted);
            ScreensManager.RootWidget = new CanvasWidget();
            ScreensManager.CurrentScreen = null;
            ScreensManager.PreviousScreen = null;
            animationField.SetValue(null, null);
            typeof(GameManager).GetMethod("BindWorldLifecycle", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [scope.Project]);
            Assert.True(authority.IsServer);

            if (closeAuthority) { scope.Project.Dispose(); }
            else { GameManager.LeaveGameplay(); }

            Assert.Null(ClientApplication.World);
            Assert.Null(game.Client);
            Assert.True(client.IsDisposed);
            if (closeAuthority)
            {
                Assert.True(authorityWorld.IsClosed);
                Assert.True(game.IsDisposed);
                Assert.Null(controller.Runtime);
                Assert.Equal(NodeRole.Inactive, authority.Role);
                Assert.Same(play, ScreensManager.CurrentScreen);
            }
            else
            {
                Assert.Same(game, controller.Runtime);
                Assert.Same(authorityWorld, game.Authority);
                Assert.False(authorityWorld.IsClosed);
                Assert.Equal(NodeRole.Authority, authority.Role);
                Assert.Same(hosted, ScreensManager.CurrentScreen);
            }
        }
        finally
        {
            try
            {
                presentation.Dispose();
                game.Dispose();
                scope.Dispose();
                client.Dispose();
                authority.StopImmediate();
            }
            finally
            {
                runtimeProperty.SetValue(controller, previousRuntime);
                infoProperty.SetValue(controller, previousInfo);
                sessionProperty.SetValue(controller, previousSession);
                controller.LocalPlayerReturnTarget = previousReturn;
                nodeField.SetValue(null, previousNode);
                presentationField.SetValue(null, previousPresentation);
                ScreensManager.Screens = previousScreens;
                rootField.SetValue(null, previousRoot);
                ScreensManager.CurrentScreen = previousCurrent;
                ScreensManager.PreviousScreen = previousPrevious;
                animationField.SetValue(null, previousAnimation);
            }
        }
    }
}
