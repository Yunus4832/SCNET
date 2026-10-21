using Engine.Core.Runtime;

using Game;
using Game.Managers;

namespace Survivalcraft.Test;

public sealed class GameExitManagerTest
{
    [Fact]
    public void RequestUpdateHandsVerifiedPackageToStarterAndResetsForNextSession()
    {
        var previousRunMode = RunMode.Value;
        var packagePath = Path.Combine(Path.GetTempPath(), "verified-update.zip");
        const string downloadUrl = "https://example.com/release.zip";
        var requestedActions = new List<GameExitAction>();

        void OnExitRequested(GameExitAction action)
        {
            Assert.Equal(packagePath, GameExitManager.UpdatePackagePath);
            Assert.Equal(downloadUrl, GameExitManager.UpdateDownloadUrl);
            requestedActions.Add(action);
        }

        try
        {
            RunMode.Value = RunModeType.HeadlessServer;
            GameExitManager.BeginSession();
            GameExitManager.ExitRequested += OnExitRequested;

            GameExitManager.RequestUpdate(packagePath, downloadUrl);

            Assert.Equal(GameExitAction.Update, GameExitManager.ExitAction);
            Assert.Equal([GameExitAction.Update], requestedActions);
            GameExitManager.BeginSession();
            Assert.Equal(GameExitAction.Exit, GameExitManager.ExitAction);
            Assert.Empty(GameExitManager.UpdatePackagePath);
            Assert.Empty(GameExitManager.UpdateDownloadUrl);
        }
        finally
        {
            GameExitManager.ExitRequested -= OnExitRequested;
            RunMode.Value = previousRunMode;
            GameExitManager.BeginSession();
        }
    }

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
