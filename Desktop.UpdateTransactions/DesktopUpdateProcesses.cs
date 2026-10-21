using System.Diagnostics;
using System.Xml.Linq;

namespace Scnet.Desktop.Updates;

public static class DesktopUpdateProcesses
{
    public static bool IsAlive(int pid, long startTimeTicks)
    {
        if (pid <= 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited &&
                   Math.Abs(process.StartTime.ToUniversalTime().Ticks - startTimeTicks) <
                   TimeSpan.TicksPerSecond;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static void RequireOtherInstancesStopped(string directory, int allowedPid)
    {
        var instances = Path.Combine(directory, "Instances");
        if (!Directory.Exists(instances))
        {
            return;
        }

        foreach (var instance in Directory.EnumerateDirectories(instances))
        {
            var runtime = Path.Combine(instance, ".runtime");
            if (!Directory.Exists(runtime))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(runtime, "*.xml"))
            {
                var marker = XElement.Load(path);
                var pid = (int?)marker.Attribute("Pid") ?? throw new InvalidDataException("Invalid process marker.");
                var ticks = (long?)marker.Attribute("StartTimeUtcTicks") ??
                            throw new InvalidDataException("Invalid process marker.");
                if (pid != allowedPid && IsAlive(pid, ticks))
                {
                    throw new IOException("Close all other SCNET instances before updating.");
                }
            }
        }
    }
}
