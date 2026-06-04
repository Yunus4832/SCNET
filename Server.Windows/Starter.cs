using Game;

namespace Server.Windows;

public class Starter
{
    public static void Main(string[] args)
    {
        RunMode.Value = RunModeType.HeadlessServer;
        ServerProgram.Main(args);
    }
}
