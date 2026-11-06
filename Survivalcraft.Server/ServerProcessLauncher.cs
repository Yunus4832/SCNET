using System.Diagnostics;
using System.Reflection;

namespace Survivalcraft.Server;

internal static class ServerProcessLauncher
{
    public static void Restart(string instanceId)
    {
        var processPath = Environment.ProcessPath ??
                          throw new InvalidOperationException("The server executable path is unavailable.");
        var start = new ProcessStartInfo(processPath)
        {
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
        }

        start.ArgumentList.Add("--instance");
        start.ArgumentList.Add(instanceId);
        using var next = Process.Start(start) ??
                         throw new InvalidOperationException("The server restart did not start a process.");
    }
}
