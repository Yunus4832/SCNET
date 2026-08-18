using Engine.Core;

using Game;

namespace Survivalcraft.Test.Lifecycle;

public sealed class GameExitManagerTest
{
    [Fact]
    public void RequestExitPublishesExitAndStopsThroughLifecycleManager()
    {
        var previousRunMode = RunMode.Value;
        var requestedActions = new List<GameExitAction>();
        void OnExitRequested(GameExitAction action) => requestedActions.Add(action);

        try
        {
            RunMode.Value = RunModeType.HeadlessServer;
            GameExitManager.BeginSession();
            GameExitManager.ExitRequested += OnExitRequested;

            GameExitManager.RequestExit();

            Assert.Equal(GameExitAction.Exit, GameExitManager.ExitAction);
            Assert.Equal([GameExitAction.Exit], requestedActions);
        }
        finally
        {
            GameExitManager.ExitRequested -= OnExitRequested;
            RunMode.Value = previousRunMode;
            GameExitManager.BeginSession();
        }
    }
}
