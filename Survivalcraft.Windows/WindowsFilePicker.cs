using System.Diagnostics;
using System.Text.Json;

using Engine.FileStorage;

namespace Survivalcraft.Windows;

internal sealed class WindowsFilePicker : IFilePicker
{
    public async Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerRequest request,
        CancellationToken cancellationToken = default)
    {
        var script = """
            Add-Type -AssemblyName System.Windows.Forms
            $dialog = [System.Windows.Forms.OpenFileDialog]::new()
            $dialog.Title = $env:SCNET_PICKER_TITLE
            $dialog.Filter = $env:SCNET_PICKER_FILTER
            $dialog.Multiselect = $env:SCNET_PICKER_MULTIPLE -eq '1'
            if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
              $dialog.FileNames | ConvertTo-Json -Compress
            }
            """;
        var environment = new Dictionary<string, string?>
        {
            ["SCNET_PICKER_TITLE"] = request.Title ?? string.Empty,
            ["SCNET_PICKER_FILTER"] = BuildFilter(request.Extensions),
            ["SCNET_PICKER_MULTIPLE"] = request.AllowMultiple ? "1" : "0"
        };
        var output = await RunPowerShellAsync(script, environment, cancellationToken);
        if (string.IsNullOrWhiteSpace(output)) return [];
        using var document = JsonDocument.Parse(output);
        var paths = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().Select(item => item.GetString()!).ToArray()
            : [document.RootElement.GetString()!];
        return paths.Select(path => new PickedFile(Path.GetFileName(path), null,
            _ => Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan)))).ToArray();
    }

    public async Task<PickedSaveTarget?> PickSaveTargetAsync(FileSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var script = """
            Add-Type -AssemblyName System.Windows.Forms
            $dialog = [System.Windows.Forms.SaveFileDialog]::new()
            $dialog.Title = $env:SCNET_PICKER_TITLE
            $dialog.FileName = $env:SCNET_PICKER_NAME
            if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
              $dialog.FileName | ConvertTo-Json -Compress
            }
            """;
        var output = await RunPowerShellAsync(script, new Dictionary<string, string?>
        {
            ["SCNET_PICKER_TITLE"] = request.Title ?? string.Empty,
            ["SCNET_PICKER_NAME"] = request.SuggestedFileName
        }, cancellationToken);
        if (string.IsNullOrWhiteSpace(output)) return null;
        var path = JsonSerializer.Deserialize<string>(output)!;
        return new PickedSaveTarget(Path.GetFileName(path),
            _ => Task.FromResult<Stream>(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan)));
    }

    private static async Task<string> RunPowerShellAsync(string script,
        IReadOnlyDictionary<string, string?> environment, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-STA");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(script);
        foreach (var pair in environment) start.Environment[pair.Key] = pair.Value;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start Windows file picker.");
        using var registration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
        });
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0) throw new InvalidOperationException($"Windows file picker failed: {error.Trim()}");
        return output.Trim();
    }

    private static string BuildFilter(IReadOnlyList<string> extensions)
    {
        var patterns = extensions.Select(extension => $"*{NormalizeExtension(extension)}").ToArray();
        return patterns.Length == 0 ? "All files (*.*)|*.*" : $"Supported files|{string.Join(';', patterns)}|All files (*.*)|*.*";
    }

    private static string NormalizeExtension(string extension) => extension.StartsWith('.') ? extension : "." + extension;
}
