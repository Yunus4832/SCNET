namespace Game;

public enum GameExitAction
{
    Exit,
    Restart
}

public static class GameExitManager
{
    public static GameExitAction ExitAction { get; private set; }

    public static event Action<GameExitAction>? ExitRequested;

    public static void RequestRestart()
    {
        ExitAction = GameExitAction.Restart;
        ExitRequested?.Invoke(ExitAction);
        Window.Close();
    }

    internal static void BeginSession()
    {
        ExitAction = GameExitAction.Exit;
    }
}
