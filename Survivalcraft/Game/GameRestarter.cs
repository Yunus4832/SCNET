namespace Game;

public class GameRestarter
{
#if ANDROID
    public static event Action? OnRestartAppRequested;
#endif

    // 重启游戏进程方法
    // 这个函数在一些情况下特别有用，比如说在帮助玩家自动添加模组时
    // Android版本的原理是先跳转到独立进程的RestartActivity，然后主进程退出
    // RestartActivity再启动主进程，然后RestartActivity退出
    // Windows版本的原理是先记录下可执行程序的位置，然后启动新的游戏进程，同时使之前的游戏进程退出
    // 有些Android版本在附加了调试器的情况下执行重启方法时会直接退出
    // 推测是调试器在主进程退出时停止调试，同时把其他进程也关闭了
    // 无论如何，IDE默认调试器无法再调试重启之后的游戏进程，因为进程变了
    public static void RestartGame()
    {
#if ANDROID
        // 调用MainActivity中的重启应用函数
        OnRestartAppRequested?.Invoke();
#endif
#if DESKTOP
        var appPath = Process.GetCurrentProcess().MainModule?.FileName;

        var restartInfo = new ProcessStartInfo
        {
            FileName = appPath,
            Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1)) // 传递原始参数（除去执行文件名）
        };

        Process.Start(restartInfo);

        Environment.Exit(0);
#endif
    }
}
