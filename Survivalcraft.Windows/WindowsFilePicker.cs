using Engine.FileStorage;

namespace Survivalcraft.Windows;

internal sealed class WindowsFilePicker : IFilePicker
{
    private readonly SemaphoreSlim _requestLock = new(1, 1);

    public async Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var paths = await RunDialogAsync(() =>
        {
            using var dialog = new OpenFileDialog();
            dialog.Title = request.Title ?? string.Empty;
            dialog.Filter = BuildFilter(request.Extensions);
            dialog.Multiselect = request.AllowMultiple;
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            dialog.RestoreDirectory = true;
            using var cancellation = cancellationToken.Register(dialog.Dispose);
            var result = dialog.ShowDialog();
            cancellationToken.ThrowIfCancellationRequested();
            return result == DialogResult.OK ? dialog.FileNames : [];
        }, cancellationToken).ConfigureAwait(false);

        return paths.Select(path => new PickedFile(Path.GetFileName(path), null,
            _ => Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan)))).ToArray();
    }

    public async Task<PickedSaveTarget?> PickSaveTargetAsync(FileSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var path = await RunDialogAsync(() =>
        {
            using var dialog = new SaveFileDialog
            {
                Title = request.Title ?? string.Empty,
                FileName = request.SuggestedFileName,
                Filter = BuildSaveFilter(request.SuggestedFileName),
                AddExtension = true,
                CheckPathExists = true,
                OverwritePrompt = true,
                RestoreDirectory = true
            };
            using var cancellation = cancellationToken.Register(dialog.Dispose);
            var result = dialog.ShowDialog();
            cancellationToken.ThrowIfCancellationRequested();
            return result == DialogResult.OK ? dialog.FileName : null;
        }, cancellationToken).ConfigureAwait(false);

        if (path is null)
        {
            return null;
        }

        return new PickedSaveTarget(Path.GetFileName(path),
            _ => Task.FromResult<Stream>(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan)));
    }

    private async Task<T> RunDialogAsync<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RunOnStaThreadAsync(operation, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private static Task<T> RunOnStaThreadAsync<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                completion.TrySetResult(operation());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "SCNET Windows File Picker"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static string BuildFilter(IReadOnlyList<string> extensions)
    {
        var patterns = extensions.Select(extension => $"*{NormalizeExtension(extension)}").ToArray();
        return patterns.Length == 0
            ? "All files (*.*)|*.*"
            : $"Supported files ({string.Join(", ", patterns)})|{string.Join(';', patterns)}|All files (*.*)|*.*";
    }

    private static string BuildSaveFilter(string suggestedFileName)
    {
        var extension = Path.GetExtension(suggestedFileName);
        return string.IsNullOrEmpty(extension)
            ? "All files (*.*)|*.*"
            : $"{extension.TrimStart('.').ToUpperInvariant()} files (*{extension})|*{extension}|All files (*.*)|*.*";
    }

    private static string NormalizeExtension(string extension) =>
        extension.StartsWith('.') ? extension : "." + extension;
}
