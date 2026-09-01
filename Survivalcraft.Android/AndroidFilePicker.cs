using Android.Content;
using Android.Provider;

using AndroidActivity = Android.App.Activity;
using AndroidUri = Android.Net.Uri;

namespace Survivalcraft.Android;

internal sealed class AndroidFilePicker(AndroidActivity activity) : IFilePicker
{
    internal const int openRequestCode = 43101;
    internal const int saveRequestCode = 43102;

    private readonly Lock _sync = new();
    private TaskCompletionSource<IReadOnlyList<PickedFile>>? _openCompletion;
    private TaskCompletionSource<PickedSaveTarget?>? _saveCompletion;
    private CancellationTokenRegistration _cancellationRegistration;

    public Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureIdle();
            _openCompletion = new TaskCompletionSource<IReadOnlyList<PickedFile>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _cancellationRegistration = cancellationToken.Register(CancelPending);
            var intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("*/*");
            intent.PutExtra(Intent.ExtraAllowMultiple, request.AllowMultiple);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
            activity.StartActivityForResult(Intent.CreateChooser(intent, request.Title), openRequestCode);
            return _openCompletion.Task;
        }
    }

    public Task<PickedSaveTarget?> PickSaveTargetAsync(FileSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureIdle();
            _saveCompletion = new TaskCompletionSource<PickedSaveTarget?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _cancellationRegistration = cancellationToken.Register(CancelPending);
            var intent = new Intent(Intent.ActionCreateDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType(request.ContentType ?? "application/octet-stream");
            intent.PutExtra(Intent.ExtraTitle, request.SuggestedFileName);
            intent.AddFlags(ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantPersistableUriPermission);
            activity.StartActivityForResult(Intent.CreateChooser(intent, request.Title), saveRequestCode);
            return _saveCompletion.Task;
        }
    }

    internal bool HandleActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        lock (_sync)
        {
            if (requestCode == openRequestCode && _openCompletion is not null)
            {
                var completion = _openCompletion;
                try
                {
                    completion.TrySetResult(resultCode == Result.Ok ? ReadPickedFiles(data) : []);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }

                ClearPending();
                return true;
            }

            if (requestCode == saveRequestCode && _saveCompletion is not null)
            {
                var completion = _saveCompletion;
                try
                {
                    completion.TrySetResult(resultCode == Result.Ok ? ReadSaveTarget(data) : null);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }

                ClearPending();
                return true;
            }

            return requestCode is openRequestCode or saveRequestCode;
        }
    }

    private IReadOnlyList<PickedFile> ReadPickedFiles(Intent? data)
    {
        var uris = new List<AndroidUri>();
        if (data?.ClipData is { } clipData)
        {
            for (var index = 0; index < clipData.ItemCount; index++)
            {
                var uri = clipData.GetItemAt(index)?.Uri;
                if (uri is not null)
                {
                    uris.Add(uri);
                }
            }
        }
        else if (data?.Data is { } uri)
        {
            uris.Add(uri);
        }

        return uris.Select(uri =>
        {
            PersistPermission(uri, ActivityFlags.GrantReadUriPermission);
            return new PickedFile(GetDisplayName(uri), activity.ContentResolver?.GetType(uri), cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(activity.ContentResolver?.OpenInputStream(uri)
                                       ?? throw new IOException("Unable to open selected document."));
            });
        }).ToArray();
    }

    private PickedSaveTarget? ReadSaveTarget(Intent? data)
    {
        var uri = data?.Data;
        if (uri is null)
        {
            return null;
        }

        PersistPermission(uri, ActivityFlags.GrantWriteUriPermission);
        return new PickedSaveTarget(GetDisplayName(uri), cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(activity.ContentResolver?.OpenOutputStream(uri, "wt")
                                   ?? throw new IOException("Unable to open selected save document."));
        });
    }

    private string GetDisplayName(AndroidUri uri)
    {
        using var cursor = activity.ContentResolver?.Query(uri, [IOpenableColumns.DisplayName], null, null, null);
        if (cursor is not null && cursor.MoveToFirst())
        {
            var index = cursor.GetColumnIndex(IOpenableColumns.DisplayName);
            if (index >= 0)
            {
                return cursor.GetString(index) ?? "document";
            }
        }

        return uri.LastPathSegment ?? "document";
    }

    private void PersistPermission(AndroidUri uri, ActivityFlags flag)
    {
        try
        {
            activity.ContentResolver?.TakePersistableUriPermission(uri, flag);
        }
        catch (Java.Lang.SecurityException)
        {
        }
    }

    private void EnsureIdle()
    {
        if (_openCompletion is not null || _saveCompletion is not null)
        {
            throw new InvalidOperationException("A file picker request is already active.");
        }
    }

    private void CancelPending()
    {
        lock (_sync)
        {
            _openCompletion?.TrySetCanceled();
            _saveCompletion?.TrySetCanceled();
            ClearPending(disposeRegistration: false);
        }
    }

    private void ClearPending(bool disposeRegistration = true)
    {
        if (disposeRegistration)
        {
            _cancellationRegistration.Dispose();
        }

        _cancellationRegistration = default;
        _openCompletion = null;
        _saveCompletion = null;
    }
}
