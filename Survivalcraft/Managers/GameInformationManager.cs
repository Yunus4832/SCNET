using System.Security.Cryptography;

using Game.Content;

namespace Game.Managers;

public static class GameInformationManager
{
    private static IReadOnlyList<GameAnnouncement> _announcements = [];

    private static Guid _loadedSourceId;

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

        _ = RefreshAnnouncementsAsync(source);
        _ = CheckReleaseAsync(source, false);
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
            _ = CheckReleaseAsync(source, true);
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
        try
        {
            ContentServerClientPool.Shared.Update(Guid.Empty,
                ContentRepositoryManager.Current.Snapshot());
            using var lease = ContentServerClientPool.Shared.Acquire(Guid.Empty, source.Id);
            var announcements = await lease.Client.ListGameAnnouncementsAsync();
            Dispatcher.Dispatch(() =>
            {
                if (ContentRepositoryManager.Current.GameInformationSource?.Id != source.Id)
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

    private static async Task CheckReleaseAsync(ContentRepository source, bool manual)
    {
        try
        {
            ContentServerClientPool.Shared.Update(Guid.Empty,
                ContentRepositoryManager.Current.Snapshot());
            using var lease = ContentServerClientPool.Shared.Acquire(Guid.Empty, source.Id);
            var release = await lease.Client.GetLatestGameReleaseAsync(PlatformKey());
            Dispatcher.Dispatch(() =>
            {
                if (ContentRepositoryManager.Current.GameInformationSource?.Id != source.Id)
                {
                    return;
                }

                if (release is null || !Version.TryParse(release.Version, out var version) ||
                    !Version.TryParse(VersionsManager.Version, out var current) || version.CompareTo(current) <= 0)
                {
                    if (manual)
                    {
                        DialogsManager.Alert(Text("Updates"), Text("UpToDate"));
                    }

                    return;
                }

                ShowRelease(source, release);
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or
                                              InvalidDataException or System.Text.Json.JsonException)
        {
            Log.Warning($"Could not check game release from '{source.Name}' ({source.BaseUrl}): {exception.Message}");
            if (manual)
            {
                ShowReleaseCheckFailure(source);
            }
        }
        catch (Exception exception)
        {
            Log.Error($"Unexpected game release failure from '{source.Name}': {exception}");
            if (manual)
            {
                ShowReleaseCheckFailure(source);
            }
        }
    }

    private static void ShowReleaseCheckFailure(ContentRepository source)
    {
        Dispatcher.Dispatch(() =>
        {
            if (ContentRepositoryManager.Current.GameInformationSource?.Id == source.Id)
            {
                DialogsManager.Alert(Text("Updates"), string.Format(Text("ReleaseCheckFailed"), source.Name));
            }
        });
    }

    private static void ShowRelease(ContentRepository source, GameRelease release)
    {
        if (!Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out var downloadUri) ||
            downloadUri.Scheme is not ("http" or "https"))
        {
            Log.Warning($"Content server '{source.Name}' returned an invalid release download URL.");
            return;
        }

        var message = string.Format(Text("ReleaseDetails"), source.Name, release.Version,
            release.Description, downloadUri.Host);
        DialogsManager.ShowDialog(null, new MessageDialog(Text("Updates"), message,
            PlatformManager.CanInstallApk ? Text("Download") : Text("OpenLink"),
            LanguageManager.Get("Usual", "no"), button =>
            {
                if (button == MessageDialogButton.Button1)
                {
                    if (PlatformManager.CanInstallApk)
                    {
                        ConfirmDownload(source, release);
                    }
                    else
                    {
                        WebBrowserManager.LaunchBrowser(release.DownloadUrl);
                    }
                }
            }));
    }

    private static void ConfirmDownload(ContentRepository source, GameRelease release)
    {
        DialogsManager.ShowDialog(null, new MessageDialog(Text("Download"),
            string.Format(Text("ConfirmDownload"), source.Name, release.Version),
            LanguageManager.Get("Usual", "yes"), LanguageManager.Get("Usual", "no"), button =>
            {
                if (button == MessageDialogButton.Button1)
                {
                    StartDownload(source, release);
                }
            }));
    }

    private static void StartDownload(ContentRepository source, GameRelease release)
    {
        var cancellation = new CancellationTokenSource();
        var dialog = new CancellableBusyDialog(Text("Downloading"), true);
        dialog.Progress.Cancelled += cancellation.Cancel;
        DialogsManager.ShowDialog(null, dialog);
        _ = DownloadAsync(source, release, dialog, cancellation);
    }

    private static async Task DownloadAsync(ContentRepository source, GameRelease release,
        CancellableBusyDialog dialog, CancellationTokenSource cancellation)
    {
        string? path = null;
        try
        {
            const long maximumDownloadSize = 1024L * 1024 * 1024;
            if (release.Sha256.Length != 64 || !Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https"))
            {
                throw new InvalidDataException("Invalid release metadata.");
            }

            var directory = PlatformManager.ApkDownloadDirectory;
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, $"{Guid.NewGuid():N}.apk");
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead,
                cancellation.Token);
            response.EnsureSuccessStatusCode();
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength > maximumDownloadSize)
            {
                throw new InvalidDataException("Release exceeds maximum download size.");
            }

            dialog.Progress.Total = contentLength ?? 0;
            await using var input = await response.Content.ReadAsStreamAsync(cancellation.Token);
            await using var output = File.Create(path);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var bytes = new byte[64 * 1024];
            long count = 0;
            int read;
            while ((read = await input.ReadAsync(bytes, cancellation.Token)) != 0)
            {
                count += read;
                if (count > maximumDownloadSize)
                {
                    throw new InvalidDataException("Release exceeds maximum download size.");
                }

                hash.AppendData(bytes, 0, read);
                await output.WriteAsync(bytes.AsMemory(0, read), cancellation.Token);
                dialog.Progress.Completed = count;
            }

            await output.FlushAsync(cancellation.Token);
            if (!Convert.ToHexString(hash.GetHashAndReset())
                    .Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Release SHA-256 mismatch.");
            }

            var verifiedPath = path;
            Dispatcher.Dispatch(() =>
            {
                DialogsManager.HideDialog(dialog);
                DialogsManager.ShowDialog(null, new MessageDialog(Text("Install"),
                    string.Format(Text("ConfirmInstall"), source.Name, release.Version),
                    LanguageManager.Get("Usual", "yes"), LanguageManager.Get("Usual", "no"), button =>
                    {
                        if (button == MessageDialogButton.Button1)
                        {
                            try
                            {
                                PlatformManager.InstallApk(verifiedPath);
                            }
                            catch (Exception exception)
                            {
                                Log.Error($"APK installation request failed: {exception}");
                                DialogsManager.Alert(Text("Install"), Text("InstallFailed"));
                            }
                        }
                        else
                        {
                            try
                            {
                                File.Delete(verifiedPath);
                            }
                            catch (Exception exception)
                            {
                                Log.Warning($"Could not remove unused APK '{verifiedPath}': {exception}");
                            }
                        }
                    }));
            });
            path = null;
        }
        catch (OperationCanceledException exception)
        {
            var userCancelled = cancellation.IsCancellationRequested;
            if (!userCancelled)
            {
                Log.Error($"Game release download timed out: {exception}");
            }

            Dispatcher.Dispatch(() =>
            {
                DialogsManager.HideDialog(dialog);
                if (!userCancelled)
                {
                    DialogsManager.Alert(Text("Download"), Text("DownloadFailed"));
                }
            });
        }
        catch (Exception exception)
        {
            Log.Error($"Game release download failed: {exception}");
            Dispatcher.Dispatch(() =>
            {
                DialogsManager.HideDialog(dialog);
                DialogsManager.Alert(Text("Download"), Text("DownloadFailed"));
            });
        }
        finally
        {
            dialog.Progress.Cancelled -= cancellation.Cancel;
            if (path is not null)
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception exception)
                {
                    Log.Warning($"Could not remove partial APK download '{path}': {exception}");
                }
            }

            cancellation.Dispose();
        }
    }

    private static string PlatformKey() => PlatformManager.Platform is Platform.Android
        ? System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture is
            System.Runtime.InteropServices.Architecture.Arm
            ? "android-arm32"
            : "android-arm64"
        : OperatingSystem.IsWindows()
            ? "windows-x64"
            : "linux-x64";

    private static string Text(string key) => LanguageManager.GetContentWidgets(nameof(GameInformationManager), key);
}
