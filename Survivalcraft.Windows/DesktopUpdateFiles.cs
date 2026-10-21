namespace Survivalcraft.Windows;

internal static class DesktopUpdateFiles
{
    internal static void Replace(string source, string destination)
    {
        var temporary = destination + ".update-" + Guid.NewGuid().ToString("N");
        try
        {
            File.Copy(source, temporary, true);
            for (var attempt = 0;; attempt++)
            {
                try
                {
                    File.Move(temporary, destination, true);
                    return;
                }
                catch (IOException) when (attempt < 20)
                {
                    Thread.Sleep(100);
                }
            }
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    internal static void Notify(string message, bool error)
    {
        MessageBox.Show(
            message,
            "SCNET update",
            MessageBoxButtons.OK,
            error ? MessageBoxIcon.Error : MessageBoxIcon.Information
        );
    }
}
