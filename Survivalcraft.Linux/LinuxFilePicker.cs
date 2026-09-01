using System.Diagnostics;

using Engine.FileStorage;

namespace Survivalcraft.Linux;

internal sealed class LinuxFilePicker : IFilePicker
{
    public static bool IsSupported => FindExecutable("zenity") is not null;

    public async Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerRequest request,
        CancellationToken cancellationToken = default)
    {
        var arguments = new List<string> { "--file-selection" };
        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            arguments.Add($"--title={request.Title}");
        }

        if (request.AllowMultiple)
        {
            arguments.Add("--multiple");
            arguments.Add("--separator=\n");
        }

        if (request.Extensions.Count > 0)
        {
            arguments.Add(
                $"--file-filter=Supported files | {string.Join(' ', request.Extensions.Select(extension => $"*{NormalizeExtension(extension)}"))}");
        }

        var output = await RunAsync(arguments, cancellationToken);
        if (output is null)
        {
            return [];
        }

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(path => new PickedFile(Path.GetFileName(path), null,
                _ => Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                    64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan)))).ToArray();
    }

    public async Task<PickedSaveTarget?> PickSaveTargetAsync(FileSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var arguments = new List<string>
        {
            "--file-selection", "--save", "--confirm-overwrite",
            $"--filename={request.SuggestedFileName}"
        };
        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            arguments.Add($"--title={request.Title}");
        }

        var path = await RunAsync(arguments, cancellationToken);
        if (path is null)
        {
            return null;
        }

        return new PickedSaveTarget(Path.GetFileName(path),
            _ => Task.FromResult<Stream>(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan)));
    }

    private static async Task<string?> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var executable = FindExecutable("zenity")
                         ?? throw new InvalidOperationException(
                             "The desktop file picker is unavailable because zenity is not installed.");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ??
                            throw new InvalidOperationException("Unable to start Linux file picker.");
        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
            }
            catch
            {
            }
        });
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode switch
        {
            0 => output.TrimEnd('\r', '\n'),
            1 => null,
            _ => throw new InvalidOperationException("Linux file picker failed to open.")
        };
    }

    private static string? FindExecutable(string name)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                 .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var path = Path.Combine(directory, name);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string NormalizeExtension(string extension) =>
        extension.StartsWith('.') ? extension : "." + extension;
}
