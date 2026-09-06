using System.Xml.Linq;

using Content.Packaging;

using Game.Content;
using Game.Modding;

namespace Game.Screens;

public sealed class OnlineContentVersionScreen : Screen
{
    private readonly ButtonWidget _downloadButton;
    private readonly LabelWidget _headerLabel;
    private readonly ButtonWidget _refreshButton;
    private readonly ButtonWidget _sourceButton;
    private readonly LabelWidget _statusLabel;
    private readonly ListPanelWidget _versionList;
    private AggregatedContentEntry? _entry;
    private bool _busy;
    private CancellationTokenSource? _cancellation;
    private OnlineContentNavigation? _navigation;
    private string _returnScreenName = "Content";
    private string? _selectedHash;
    private ContentSourceId? _selectedSource;
    private OnlineContentCatalogState _state = new();

    public OnlineContentVersionScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/OnlineContentVersionScreen"));
        _headerLabel = Children.Find<LabelWidget>("Header")!;
        _versionList = Children.Find<ListPanelWidget>("VersionList")!;
        _sourceButton = Children.Find<ButtonWidget>("Source")!;
        _downloadButton = Children.Find<ButtonWidget>("Download")!;
        _refreshButton = Children.Find<ButtonWidget>("Refresh")!;
        _statusLabel = Children.Find<LabelWidget>("Status")!;
        _versionList.ItemWidgetFactory = CreateVersionWidget;
    }

    public override void Enter(object[] parameters)
    {
        _entry = parameters.FirstOrDefault() as AggregatedContentEntry ??
                 throw new ArgumentException("Online content details require a catalog entry.", nameof(parameters));
        _navigation = parameters.Skip(1).FirstOrDefault() as OnlineContentNavigation;
        _returnScreenName = _navigation?.Normalize().ReturnScreen ?? "Content";
        _headerLabel.Text = $"{_entry.Name} ({_entry.Identifier})";
        _selectedSource = null;
        Refresh();
    }

    public override void Leave()
    {
        CancelRequest();
        _busy = false;
        _versionList.SelectedItem = null;
    }

    public override void Update()
    {
        var selected = _versionList.SelectedItem as OnlineContentVersionState;
        if (selected is not null && !string.Equals(selected.Version.PackageHash, _selectedHash,
                StringComparison.Ordinal))
        {
            _selectedHash = selected.Version.PackageHash;
            _selectedSource = null;
        }

        _sourceButton.Text = GetSourceButtonText(selected);
        _sourceButton.IsEnabled = !_busy && selected?.Version.Sources.Count > 0;
        _downloadButton.IsEnabled = !_busy && selected is not null && !selected.IsCached;
        _refreshButton.IsEnabled = !_busy;

        if (_sourceButton.IsClicked && selected is not null)
        {
            SelectSource(selected.Version);
        }

        if (_downloadButton.IsClicked && selected is not null)
        {
            Download(selected.Version);
        }

        if (_refreshButton.IsClicked)
        {
            Refresh();
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("OnlineContent",
                new OnlineContentNavigation(ReturnScreen: _returnScreenName));
        }
    }

    private Widget CreateVersionWidget(object item)
    {
        var state = (OnlineContentVersionState)item;
        var version = state.Version;
        var widget = (ContainerWidget)LoadWidget(this,
            ContentManager.Get<XElement>("Widgets/OnlineContentVersionItem"), null);
        widget.Children.Find<LabelWidget>("OnlineContentVersionItem.Version")!.Text = version.Version;
        widget.Children.Find<LabelWidget>("OnlineContentVersionItem.Hash")!.Text = version.PackageHash;
        widget.Children.Find<LabelWidget>("OnlineContentVersionItem.Details")!.Text =
            $"{DataSizeFormatter.Format(version.PackageSize)} | " +
            string.Join(", ", version.Sources.Select(source => source.RepositoryName).Distinct());
        widget.Children.Find<LabelWidget>("OnlineContentVersionItem.State")!.Text =
            string.Join(" · ", GetStateLabels(state));
        return widget;
    }

    private IEnumerable<string> GetStateLabels(OnlineContentVersionState state)
    {
        if (_entry?.Versions.FirstOrDefault() is { } latest &&
            string.Equals(latest.Version, state.Version.Version, StringComparison.Ordinal) &&
            string.Equals(latest.PackageHash, state.Version.PackageHash, StringComparison.Ordinal))
        {
            yield return Text("Latest");
        }

        if (state.IsCached)
        {
            yield return Text("Cached");
        }

        if (state.IsProfileReferenced)
        {
            yield return Text("Profile");
        }

        if (state.IsCurrentSessionReferenced)
        {
            yield return Text("CurrentSession");
        }

        if (state.Version.HasHashConflict)
        {
            yield return Text("Conflict");
        }

        if (!state.IsCached && (state.IsProfileReferenced || state.IsCurrentSessionReferenced))
        {
            yield return Text("Missing");
        }
    }

    private void Refresh()
    {
        if (_entry is null)
        {
            return;
        }

        CancelRequest();
        _busy = true;
        _statusLabel.Text = Text("Loading");
        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        var context = CreateContext();
        var service = new ContentCatalogService(SettingsManager.ContentClients);
        var entry = _entry;
        Task.Run(() => service.QueryVersionsAsync(context, entry, cancellation.Token), cancellation.Token)
            .ContinueWith(task => Dispatcher.Dispatch(() => CompleteRefresh(task, cancellation)));
    }

    private void CompleteRefresh(Task<AggregatedContentDetails> task, CancellationTokenSource cancellation)
    {
        cancellation.Dispose();
        if (!ReferenceEquals(_cancellation, cancellation))
        {
            return;
        }

        _cancellation = null;
        _busy = false;
        if (task.IsCanceled)
        {
            return;
        }

        if (!task.IsCompletedSuccessfully)
        {
            Log.Error($"Online content version query failed: {task.Exception}");
            _statusLabel.Text = Text("LoadFailed");
            PopulateVersions();
            return;
        }

        _entry = task.Result.Entry;
        _statusLabel.Text = task.Result.Failures.Count > 0
            ? string.Format(Text("PartialFailure"), task.Result.Failures.Count)
            : string.Empty;
        PopulateVersions();
    }

    private void PopulateVersions()
    {
        if (_entry is null)
        {
            return;
        }

        RefreshReferences();
        _versionList.ClearItems();
        var versions = _state.GetVersions(_entry);
        foreach (var version in versions)
        {
            _versionList.AddItem(version);
        }

        var exactNavigation = _navigation?.Normalize();
        var exactHash = exactNavigation?.PackageHash;
        _navigation = null;
        var requestedHash = exactHash ?? _selectedHash;
        var exactVersion = versions.FirstOrDefault(version =>
            string.Equals(version.Version.PackageHash, requestedHash, StringComparison.Ordinal) &&
            (exactNavigation?.Version is null ||
             string.Equals(version.Version.Version, exactNavigation.Version, StringComparison.Ordinal)));
        _versionList.SelectedItem = exactVersion ?? versions.FirstOrDefault();
        if (versions.Count == 0)
        {
            _statusLabel.Text = Text("NoVersions");
        }
        else if (exactHash is not null && exactVersion is null)
        {
            _statusLabel.Text = Text("ExactNotFound");
        }
    }

    private void Download(AggregatedContentVersion version)
    {
        if (_entry is null)
        {
            return;
        }

        CancelRequest();
        _busy = true;
        _statusLabel.Text = Text("Downloading");
        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        var context = CreateContext();
        var cache = new ContentPackageCache(Storage.GetSystemPath(GamePaths.ContentPackageCache));
        var service = new ContentDownloadService(SettingsManager.ContentClients, cache);
        var entry = _entry;
        Task.Run(() => service.DownloadAsync(context, entry, version, _selectedSource, cancellation.Token),
                cancellation.Token)
            .ContinueWith(task => Dispatcher.Dispatch(() => CompleteDownload(task, cancellation)));
    }

    private void CompleteDownload(Task<ContentDownloadResult> task, CancellationTokenSource cancellation)
    {
        cancellation.Dispose();
        if (!ReferenceEquals(_cancellation, cancellation))
        {
            return;
        }

        _cancellation = null;
        _busy = false;
        if (task.IsCanceled)
        {
            return;
        }

        if (!task.IsCompletedSuccessfully)
        {
            Log.Error($"Online content download failed: {task.Exception}");
            _statusLabel.Text = Text("DownloadFailed");
            return;
        }

        PopulateVersions();
        if (task.Result.Entry.Type == ContentPackageType.Mod)
        {
            DialogsManager.Alert(Text("ModDownloaded"));
            return;
        }

        ContentPackageInstallDialogs.Show(task.Result.Entry, busy => _busy = busy,
            () => DialogsManager.Alert(Text("Installed")),
            exception =>
            {
                Log.Error($"Downloaded content installation failed: {exception}");
                DialogsManager.Alert(Text("InstallFailed"));
            });
    }

    private void SelectSource(AggregatedContentVersion version)
    {
        var enabled = SettingsManager.ContentRepositories.Snapshot()
            .Where(repository => repository.IsEnabled)
            .Select(repository => repository.Id)
            .ToHashSet();
        var options = new[] { new SourceOption(null, Text("AutomaticSource")) }
            .Concat(version.Sources.Where(source => enabled.Contains(source.RepositoryId))
                .Select(source => new SourceOption(new ContentSourceId(source.ScopeId, source.RepositoryId),
                    source.RepositoryName)))
            .DistinctBy(option => option.Source)
            .ToArray();
        DialogsManager.ShowDialog(null, new ListSelectionDialog(Text("SourceTitle"), options, 56f,
            item => ((SourceOption)item).Name,
            item => _selectedSource = ((SourceOption)item).Source));
    }

    private string GetSourceButtonText(OnlineContentVersionState? selected)
    {
        if (_selectedSource is null || selected is null)
        {
            return Text("AutomaticSource");
        }

        return selected.Version.Sources.FirstOrDefault(source =>
            source.ScopeId == _selectedSource.ScopeId &&
            source.RepositoryId == _selectedSource.RepositoryId)?.RepositoryName ?? Text("AutomaticSource");
    }

    private static ContentSourceContext CreateContext()
    {
        return ContentSourceContext.Persistent(SettingsManager.ContentRepositories.Snapshot());
    }

    private void RefreshReferences()
    {
        var cache = new ContentPackageCache(Storage.GetSystemPath(GamePaths.ContentPackageCache));
        cache.RebuildIndex();
        _state = new OnlineContentCatalogState();
        _state.SetReferences(cache.List().Select(entry => entry.PackageHash),
            ToReferences(ModPackageReferenceTracker.GetReferencedRequirements()),
            ToReferences(ModPackageReferenceTracker.GetCurrentRuntimeRequirements()));
    }

    private static IEnumerable<OnlineContentReference> ToReferences(
        IEnumerable<ModPackageRequirement> requirements)
    {
        return requirements.Select(requirement => new OnlineContentReference(
            ContentPackageType.Mod, requirement.ModId, requirement.PackageHash));
    }

    private void CancelRequest()
    {
        _cancellation?.Cancel();
        _cancellation = null;
    }

    private static string Text(string key)
    {
        return LanguageManager.GetContentWidgets(nameof(OnlineContentVersionScreen), key);
    }

    private sealed record SourceOption(ContentSourceId? Source, string Name);
}
