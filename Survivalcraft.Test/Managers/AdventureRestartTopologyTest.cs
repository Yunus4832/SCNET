using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Runtime;

using Game.Managers;
using Game.Network;
using Game.Screens;
using Game.Startup;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Managers;

[Collection(ConfigFileCollection.Name)]
public sealed class AdventureRestartTopologyTest
{
    [Theory]
    [InlineData(SessionTarget.World, false)]
    [InlineData(SessionTarget.HostedWorld, true)]
    public void AuthorityRoleDoesNotTurnSinglePlayerRestartIntoAListener(SessionTarget target, bool runServer)
    {
        var previousRole = CommonLib.Net.Role;
        var previousSession = GameManager.WorldSession;
        var previousOperation = ProgressManager.OperationName;
        var previousProgress = ProgressManager.Progress;
        var controller = typeof(GameManager).GetField("_worlds", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!;
        var sessionProperty = typeof(GameWorldController).GetProperty(nameof(GameWorldController.WorldSession))!;
        var screen = (GameLoadingScreen)RuntimeHelpers.GetUninitializedObject(typeof(GameLoadingScreen));
        var states = new StateMachine();
        states.AddState("WaitingForFadeIn", () => { }, () => { }, () => { });
        typeof(GameLoadingScreen).GetField("_stateMachine", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(screen, states);
        var world = new WorldInfo();
        var session = new SessionInfo { Target = target };
        try
        {
            Assert.False(CommonLib.Net.IsTransportRunning);
            CommonLib.Net.Role = NodeRole.Authority;
            sessionProperty.SetValue(controller, null);

            screen.Enter([world, "AdventureRestart", session]);

            Assert.Equal(runServer, world.Hosting.RunServer);
            Assert.Equal("WaitingForFadeIn", states.CurrentState);
        }
        finally
        {
            sessionProperty.SetValue(controller, previousSession);
            CommonLib.Net.Role = previousRole;
            ProgressManager.UpdateProgress(previousOperation, previousProgress);
        }
    }
}
