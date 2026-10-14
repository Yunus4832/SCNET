namespace Survivalcraft.Linux;

internal static class DesktopUpdateFiles
{
    internal static void Replace(string source, string destination)
    {
        var temporary = destination + ".update-" + Guid.NewGuid().ToString("N");
        try
        {
            File.Copy(source, temporary, true);
            File.Move(temporary, destination, true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    internal static void Notify(string message, bool error)
    {
        Console.Error.WriteLine(message);
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo("notify-send") { UseShellExecute = false };
            start.ArgumentList.Add(error ? "SCNET update failed" : "SCNET update");
            start.ArgumentList.Add(message);
            using var process = System.Diagnostics.Process.Start(start);
            if (process is not null && !process.WaitForExit(3000))
            {
                process.Kill();
                process.WaitForExit();
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The result remains visible in the terminal when desktop notifications are unavailable.
        }
    }
}
