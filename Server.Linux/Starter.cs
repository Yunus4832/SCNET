using Game;

namespace Server.Linux;

public class Starter
{
    public static void Main(string[] args)
    {
        RunMode.Value = RunModeType.HeadlessServer;
        ServerProgram.Main(args);
    }
}
