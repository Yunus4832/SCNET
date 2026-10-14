using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Game.Content;

using Dialog = Game.Dialogs.Dialog;

namespace Game.Managers;

public static class GameInformationManager
{
    private static IReadOnlyList<GameAnnouncement> _announcements = [];

    private static Guid _loadedSourceId;

    private static readonly Lock _updateGate = new();
    private static ContentRepositoryManager? _sourceManager;
    private static UpdateOperation? _updateOperation;
    private static readonly TimeSpan _informationTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _downloadTimeout = TimeSpan.FromMinutes(10);

    public static event Action? AnnouncementsChanged;

    public static IReadOnlyList<string> AnnouncementTitles
    {
        get
        {
            var source = ContentRepositoryManager.Current.GameInformationSource;
            if (source is null || source.Id != _loadedSourceId)
            {
                return [];
            }

            return _announcements.Select(item => item.Title).ToArray();
        }
    }

    public static void StartAutomaticCheck()
    {
        if (RunMode.Value is not RunModeType.Gui ||
            ContentRepositoryManager.Current.GameInformationSource is not { } source)
        {
            return;
        }

        ObserveSourceManager(ContentRepositoryManager.Current);
        _ = RefreshAnnouncementsAsync(source);
        StartReleaseCheck(source, false);
    }

    public static void ShowAnnouncements()
    {
        if (!TryGetSource(out var source))
        {
            return;
        }

        if (_loadedSourceId != source.Id || _announcements.Count == 0)
        {
            return;
        }

        ShowAnnouncementList();
    }

    private static void ShowAnnouncementList()
    {
        DialogsManager.ShowDialog(null, new GameAnnouncementsDialog(Text("Announcements"), _announcements));
    }

    public static void CheckManually()
    {
        if (TryGetSource(out var source))
        {
            StartReleaseCheck(source, true);
        }
    }

    private static bool TryGetSource(out ContentRepository source)
    {
        source = ContentRepositoryManager.Current.GameInformationSource!;
        if (source is not null)
        {
            return true;
        }

        DialogsManager.ShowDialog(null, new MessageDialog(Text("NoInformationSourceTitle"),
            Text("NoInformationSourceBody"),
            Text("Configure"), LanguageManager.Get("Usual", "no"), button =>
            {
                if (button == MessageDialogButton.Button1)
                {
                    ScreensManager.SwitchScreen("ContentRepositories", "MainMenu");
                }
            }));
        return false;
    }

    private static async Task RefreshAnnouncementsAsync(ContentRepository source)
    {
        var manager = ContentRepositoryManager.Current;
        var revision = manager.GameInformationSourceRevision;
        using var timeout = new CancellationTokenSource(_informationTimeout);
        try
        {
            ContentServerClientPool.Shared.Update(Guid.Empty,
                ContentRepositoryManager.Current.Snapshot());
            using var lease = ContentServerClientPool.Shared.Acquire(Guid.Empty, source.Id);
            var announcements = await lease.Client.ListGameAnnouncementsAsync(timeout.Token);
            Dispatcher.Dispatch(() =>
            {
                if (!ReferenceEquals(manager, ContentRepositoryManager.Current) ||
                    manager.GameInformationSourceRevision != revision)
                {
                    return;
                }

                _loadedSourceId = source.Id;
                _announcements = announcements;
                AnnouncementsChanged?.Invoke();
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or
                                              InvalidDataException or System.Text.Json.JsonException)
        {
            Log.Warning(
                $"Could not load game announcements from '{source.Name}' ({source.BaseUrl}): {exception.Message}");
        }
        catch (Exception exception)
        {
            Log.Error($"Unexpected game announcement failure from '{source.Name}': {exception}");
        }
    }

    private static void StartReleaseCheck(ContentRepository source, bool manual)
    {
        var operation = TryBeginUpdate(ContentRepositoryManager.Current, source, manual);
        if (operation is not null)
        {
            _ = CheckReleaseAsync(operation);
        }
    }

    internal static UpdateOperation? TryBeginUpdate(ContentRepositoryManager manager, ContentRepository source,
        bool manual)
    {
        ObserveSourceManager(manager);
        lock (_updateGate)
        {
            if (_updateOperation is not null)
            {
                _updateOperation.ManualRequested |= manual;
                return null;
            }

            _updateOperation = new UpdateOperation(manager, source, manual);
            return _updateOperation;
        }
    }

    private static void ObserveSourceManager(ContentRepositoryManager manager)
    {
        if (ReferenceEquals(_sourceManager, manager))
        {
            return;
        }

        if (_sourceManager is not null)
        {
            _sourceManager.GameInformationSourceChanged -= InvalidateSourceResults;
        }

        InvalidateSourceResults();
        _sourceManager = manager;
        manager.GameInformationSourceChanged += InvalidateSourceResults;
    }

    private static void InvalidateSourceResults()
    {
        UpdateOperation? operation;
        lock (_updateGate)
        {
            operation = _updateOperation;
        }

        if (operation is not null)
        {
            EndUpdate(operation);
        }

        _loadedSourceId = Guid.Empty;
        _announcements = [];
        AnnouncementsChanged?.Invoke();
    }

    internal static bool IsCurrent(UpdateOperation operation)
    {
        var revision = operation.Manager.GameInformationSourceRevision;
        lock (_updateGate)
        {
            return ReferenceEquals(_updateOperation, operation) &&
                   ReferenceEquals(_sourceManager, operation.Manager) &&
                   revision == operation.SourceRevision && !operation.Token.IsCancellationRequested;
        }
    }

    internal static void EndUpdate(UpdateOperation operation)
    {
        lock (_updateGate)
        {
            if (ReferenceEquals(_updateOperation, operation))
            {
                _updateOperation = null;
            }
        }

        operation.Dispose();
        if (operation.Dialog is not null)
        {
            DialogsManager.HideDialog(operation.Dialog);
            operation.Dialog = null;
        }

        if (operation.PackagePath is not null)
        {
            DeleteDownload(operation.PackagePath);
            operation.PackagePath = null;
        }
    }

    private static async Task CheckReleaseAsync(UpdateOperation operation)
    {
        var source = operation.Source;
        try
        {
            var platform = GetReleasePlatform(PlatformManager.Platform, RuntimeInformation.ProcessArchitecture,
                OperatingSystem.IsWindows());
            if (platform is null)
            {
                Dispatcher.Dispatch(() => ShowCheckResult(operation, "UnsupportedPlatform"));
                return;
            }

            ContentServerClientPool.Shared.Update(Guid.Empty, operation.Manager.Snapshot());
            using var lease = ContentServerClientPool.Shared.Acquire(Guid.Empty, source.Id);
            var release = await FetchReleaseAsync(lease.Client, platform, operation.Token, _informationTimeout);
            var result = EvaluateRelease(release, platform, VersionsManager.Version);
            Dispatcher.Dispatch(() =>
            {
                if (!IsCurrent(operation))
                {
                    EndUpdate(operation);
                    return;
                }

                if (result == ReleaseCheckResult.Available)
                {
                    ShowRelease(operation, release!);
                }
                else
                {
                    ShowCheckResult(operation,
                        result == ReleaseCheckResult.NotPublished ? "NoPublishedRelease" : "UpToDate");
                }
            });
        }
        catch (OperationCanceledException) when (operation.Token.IsCancellationRequested)
        {
            Dispatcher.Dispatch(() => EndUpdate(operation));
        }
        catch (Exception exception)
        {
            if (exception is HttpRequestException or OperationCanceledException or TimeoutException or
                InvalidDataException or System.Text.Json.JsonException)
            {
                Log.Warning($"Could not check game release from '{source.Name}' ({source.BaseUrl}): {exception.Message}");
            }
            else
            {
                Log.Error($"Unexpected game release failure from '{source.Name}': {exception}");
            }

            Dispatcher.Dispatch(() => ShowCheckResult(operation, "ReleaseCheckFailed", source.Name));
        }
    }

    internal static async Task<GameRelease?> FetchReleaseAsync(ContentServerClient client, string platform,
        CancellationToken cancellationToken, TimeSpan timeout)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            return await client.GetLatestGameReleaseAsync(platform, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Game release check exceeded its total time limit.", exception);
        }
    }

    internal static ReleaseCheckResult EvaluateRelease(GameRelease? release, string platform, string currentVersion)
    {
        if (release is null)
        {
            return ReleaseCheckResult.NotPublished;
        }

        if (release.Platform != platform || release.Status != "published" ||
            string.IsNullOrWhiteSpace(release.Id) || !Version.TryParse(release.Version, out var version) ||
            !Version.TryParse(currentVersion, out var current) ||
            !Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            release.Sha256 is not { Length: 64 } || !release.Sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("Content server returned invalid game release metadata.");
        }

        return version.CompareTo(current) > 0 ? ReleaseCheckResult.Available : ReleaseCheckResult.UpToDate;
    }

    private static void ShowCheckResult(UpdateOperation operation, string key, params object[] arguments)
    {
        if (!IsCurrent(operation) || !operation.ManualRequested)
        {
            EndUpdate(operation);
            return;
        }

        ShowUpdateDialog(operation, new MessageDialog(Text("Updates"), string.Format(Text(key), arguments),
            LanguageManager.Get("Usual", "ok"), string.Empty, _ => EndUpdate(operation)));
    }

    private static void ShowUpdateDialog(UpdateOperation operation, Dialog dialog)
    {
        if (!IsCurrent(operation))
        {
            EndUpdate(operation);
            return;
        }

        operation.Dialog = dialog;
        DialogsManager.ShowDialog(null, dialog);
    }

    private static void ShowRelease(UpdateOperation operation, GameRelease release)
    {
        var message = string.Format(Text("ReleaseDetails"), operation.Source.Name, release.Version,
            release.Description, new Uri(release.DownloadUrl).Host);
        ShowUpdateDialog(operation, new MessageDialog(Text("Updates"), message,
            PlatformManager.CanRequestUpdate ? Text("Download") : Text("OpenLink"),
            LanguageManager.Get("Usual", "no"), button =>
            {
                if (!IsCurrent(operation) || button != MessageDialogButton.Button1)
                {
                    EndUpdate(operation);
                    return;
                }

                if (PlatformManager.CanRequestUpdate)
                {
                    ConfirmDownload(operation, release);
                }
                else
                {
                    WebBrowserManager.LaunchBrowser(release.DownloadUrl);
                    EndUpdate(operation);
                }
            }));
    }

    private static void ConfirmDownload(UpdateOperation operation, GameRelease release)
    {
        ShowUpdateDialog(operation, new MessageDialog(Text("Download"),
            string.Format(Text("ConfirmDownload"), operation.Source.Name, release.Version),
            LanguageManager.Get("Usual", "yes"), LanguageManager.Get("Usual", "no"), button =>
            {
                if (IsCurrent(operation) && button == MessageDialogButton.Button1)
                {
                    StartDownload(operation, release);
                }
                else
                {
                    EndUpdate(operation);
                }
            }));
    }

    private static void StartDownload(UpdateOperation operation, GameRelease release)
    {
        var dialog = new CancellableBusyDialog(Text("Downloading"), true);
        dialog.Progress.Cancelled += operation.Cancel;
        ShowUpdateDialog(operation, dialog);
        _ = DownloadAsync(operation, release, dialog);
    }

    private static async Task DownloadAsync(UpdateOperation operation, GameRelease release,
        CancellableBusyDialog dialog)
    {
        var path = Path.Combine(PlatformManager.UpdateDownloadDirectory,
            $"{Guid.NewGuid():N}{PlatformManager.UpdateDownloadExtension}");
        try
        {
            using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            await DownloadPackageAsync(client, release, path, (completed, total) =>
            {
                dialog.Progress.Total = total ?? 0;
                dialog.Progress.Completed = completed;
            }, operation.Token, _downloadTimeout);
            Dispatcher.Dispatch(() =>
            {
                if (!IsCurrent(operation) || dialog.Progress.CancellationToken.IsCancellationRequested)
                {
                    DeleteDownload(path);
                    EndUpdate(operation);
                    return;
                }

                operation.PackagePath = path;
                DialogsManager.HideDialog(dialog);
                ShowUpdateDialog(operation, new MessageDialog(Text("Install"),
                    string.Format(Text(PlatformManager.UpdateConfirmationKey), operation.Source.Name, release.Version),
                    LanguageManager.Get("Usual", "yes"), LanguageManager.Get("Usual", "no"), button =>
                    {
                        if (IsCurrent(operation) && button == MessageDialogButton.Button1)
                        {
                            operation.PackagePath = null;
                            GameExitManager.RequestUpdate(path, release.DownloadUrl);
                        }

                        EndUpdate(operation);
                    }));
            });
        }
        catch (OperationCanceledException) when (operation.Token.IsCancellationRequested)
        {
            Dispatcher.Dispatch(() => EndUpdate(operation));
        }
        catch (Exception exception)
        {
            Log.Warning($"Game release download failed: {exception.Message}");
            Dispatcher.Dispatch(() =>
            {
                if (IsCurrent(operation) && !dialog.Progress.CancellationToken.IsCancellationRequested)
                {
                    DialogsManager.HideDialog(dialog);
                    ShowUpdateDialog(operation, new MessageDialog(Text("Download"), Text("DownloadFailed"),
                        LanguageManager.Get("Usual", "ok"), string.Empty, _ => EndUpdate(operation)));
                }
                else
                {
                    EndUpdate(operation);
                }
            });
        }
        finally
        {
            dialog.Progress.Cancelled -= operation.Cancel;
        }
    }

    internal static async Task DownloadPackageAsync(HttpClient client, GameRelease release, string path,
        Action<long, long?> progress, CancellationToken cancellationToken, TimeSpan timeout)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var token = deadline.Token;
        try
        {
            const long maximumDownloadSize = 1024L * 1024 * 1024;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var response = await client.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead,
                token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength > maximumDownloadSize)
            {
                throw new InvalidDataException("Release exceeds maximum download size.");
            }

            progress(0, contentLength);
            await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            await using var output = File.Create(path);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var bytes = new byte[64 * 1024];
            long count = 0;
            int read;
            while ((read = await input.ReadAsync(bytes, token).ConfigureAwait(false)) != 0)
            {
                count += read;
                if (count > maximumDownloadSize)
                {
                    throw new InvalidDataException("Release exceeds maximum download size.");
                }

                hash.AppendData(bytes, 0, read);
                await output.WriteAsync(bytes.AsMemory(0, read), token).ConfigureAwait(false);
                progress(count, contentLength);
            }

            await output.FlushAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (contentLength is { } expected && count != expected ||
                !Convert.ToHexString(hash.GetHashAndReset()).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Release download size or SHA-256 mismatch.");
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            DeleteDownload(path);
            throw new TimeoutException("Game release download exceeded its total time limit.", exception);
        }
        catch
        {
            DeleteDownload(path);
            throw;
        }
    }

    private static void DeleteDownload(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Warning($"Could not remove unused update '{path}': {exception.Message}");
        }
    }

    internal static string? GetReleasePlatform(Platform platform, Architecture architecture, bool windows) =>
        platform == Platform.Android
            ? architecture switch
            {
                Architecture.Arm => "android-arm32",
                Architecture.Arm64 => "android-arm64",
                _ => null
            }
            : architecture == Architecture.X64 ? windows ? "windows-x64" : "linux-x64" : null;

    internal enum ReleaseCheckResult
    {
        NotPublished,
        UpToDate,
        Available
    }

    internal sealed class UpdateOperation : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Lock _gate = new();
        private bool _disposed;

        internal UpdateOperation(ContentRepositoryManager manager, ContentRepository source, bool manual)
        {
            Manager = manager;
            Source = source;
            SourceRevision = manager.GameInformationSourceRevision;
            ManualRequested = manual;
            Token = _cancellation.Token;
        }

        internal ContentRepositoryManager Manager { get; }
        internal ContentRepository Source { get; }
        internal long SourceRevision { get; }
        internal bool ManualRequested { get; set; }
        internal Dialog? Dialog { get; set; }
        internal string? PackagePath { get; set; }
        internal CancellationToken Token { get; }

        internal void Cancel()
        {
            lock (_gate)
            {
                if (!_disposed)
                {
                    _cancellation.Cancel();
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    _cancellation.Cancel();
                    _cancellation.Dispose();
                }
            }
        }
    }

    private static string Text(string key) => LanguageManager.GetContentWidgets(nameof(GameInformationManager), key);
}
