using System.Xml.Linq;

using Content.Packaging;

using Game.Content;
using Game.Modding;

namespace Game.Screens;

public sealed class OnlineContentScreen : Screen
{
    private const int _maximumVersionCacheEntries = 32;

    private const int _pageSize = ContentCatalogQuery.DefaultPageSize;

    private static readonly TimeSpan _versionCacheDuration = TimeSpan.FromMinutes(5);

    private enum CatalogAction
    {
        Download,
        PreviousPage,
        NextPage,
        Refresh
    }

    private readonly ActionPanelWidget _actionPanel;
    private readonly ListPanelWidget _contentList;
    private readonly SelectionDrawerWidget _repositoryFilterDrawer;
    private readonly LabelWidget _searchPlaceholder;
    private readonly TextBoxWidget _searchTextBox;
    private readonly LabelWidget _statusLabel;
    private readonly SelectionDrawerWidget _statusFilterDrawer;
    private readonly SelectionDrawerWidget _typeFilterDrawer;
    private readonly SelectionDrawerWidget _versionDrawer;
    private readonly Dictionary<(ContentPackageType Type, string Identifier), CachedVersions> _versionCache = [];
    private readonly OnlineContentCatalogState _state = new();
    private bool _busy;
    private CancellationTokenSource? _cancellation;
    private bool _moveToNextPageAfterLoad;
    private int _pageIndex = 1;
    private AggregatedContentEntry? _selectedEntry;
    private string? _selectedHash;
    private OnlineContentNavigation? _versionNavigation;
    private bool _updatingContentList;
    private bool _updatingFilters;
    private bool _updatingVersions;
    private OnlineContentNavigation? _pendingNavigation;
    private Guid? _repositoryId;
    private string _returnScreenName = "Content";
    private string? _search;
    private OnlineContentStatusFilter _statusFilter;
    private ContentPackageType? _typeFilter;

    public OnlineContentScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/OnlineContentScreen"));
        _contentList = Children.Find<ListPanelWidget>("ContentList")!;
        _searchTextBox = Children.Find<TextBoxWidget>("Search")!;
        _searchPlaceholder = Children.Find<LabelWidget>("SearchPlaceholder")!;
        _typeFilterDrawer = Children.Find<SelectionDrawerWidget>("TypeFilter")!;
        _repositoryFilterDrawer = Children.Find<SelectionDrawerWidget>("RepositoryFilter")!;
        _statusFilterDrawer = Children.Find<SelectionDrawerWidget>("StatusFilter")!;
        _actionPanel = Children.Find<ActionPanelWidget>("Actions")!;
        _versionDrawer = new SelectionDrawerWidget
        {
            Size = new Vector2(140f, 60f),
            ExpansionDirection = SelectionDrawerDirection.Up,
            MaxVisibleItems = 5,
            PlaceholderText = Text("Version")
        };
        _statusLabel = Children.Find<LabelWidget>("Status")!;
        _contentList.ItemWidgetFactory = CreateContentWidget;
        _contentList.SelectionChanged += ContentSelectionChanged;
        _searchPlaceholder.Text = Text("SearchPlaceholder");
        _searchTextBox.MaximumLength = 100;
        _searchTextBox.TextChanged += _ => UpdateSearchPlaceholder();
        _searchTextBox.FocusLost += _ => ApplySearchText();
        _searchTextBox.Enter += textBox => textBox.HasFocus = false;
        _typeFilterDrawer.ItemTextProvider = item => ((ContentTypeOption)item).Type?.ToString() ?? Text("AllTypes");
        _repositoryFilterDrawer.ItemTextProvider = item => ((RepositoryOption)item).Name;
        _statusFilterDrawer.ItemTextProvider = item => Text($"Status{item}");
        _typeFilterDrawer.SelectionChanged += TypeFilterChanged;
        _repositoryFilterDrawer.SelectionChanged += RepositoryFilterChanged;
        _statusFilterDrawer.SelectionChanged += StatusFilterChanged;
        _versionDrawer.ItemTextProvider = item => ((OnlineContentVersionState)item).Version.Version;
        _versionDrawer.SelectionChanged += VersionSelectionChanged;
        _actionPanel.PrimaryAccessory = _versionDrawer;
        _actionPanel.ItemTextProvider = item => Text(item.ToString()!);
        _actionPanel.ItemEnabledProvider = IsActionEnabled;
        _actionPanel.ItemClicked += ExecuteAction;
        _actionPanel.SetPrimaryItems(
        [
            CatalogAction.Download,
            CatalogAction.PreviousPage,
            CatalogAction.NextPage
        ]);
        _actionPanel.SetSecondaryItems([CatalogAction.Refresh]);
    }

    public override void Enter(object[] parameters)
    {
        var navigation = parameters.FirstOrDefault() as OnlineContentNavigation;
        _returnScreenName = navigation?.Normalize().ReturnScreen ?? "Content";
        _pendingNavigation = navigation is { Type: not null, Identifier: not null }
            ? navigation.Normalize()
            : null;
        if (_pendingNavigation is not null)
        {
            _pendingNavigation = _pendingNavigation.Normalize();
            _typeFilter = _pendingNavigation.Type;
            _search = _pendingNavigation.Identifier;
            _repositoryId = null;
            _statusFilter = OnlineContentStatusFilter.All;
        }

        _searchTextBox.Text = _search ?? string.Empty;
        UpdateSearchPlaceholder();
        RefreshFilterDrawers();
        Refresh();
    }

    public override void Leave()
    {
        CancelRequest();
        _busy = false;
        ClearSelectedContent();
        _typeFilterDrawer.Close();
        _repositoryFilterDrawer.Close();
        _statusFilterDrawer.Close();
    }

    public override void Update()
    {
        _searchTextBox.IsEnabled = !_busy;
        _contentList.IsEnabled = !_busy;
        _typeFilterDrawer.IsEnabled = !_busy;
        _repositoryFilterDrawer.IsEnabled = !_busy;
        _statusFilterDrawer.IsEnabled = !_busy;
        _versionDrawer.IsEnabled = !_busy;
        _actionPanel.IsEnabled = !_busy;
        _actionPanel.Refresh();

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(_returnScreenName);
        }
    }

    private static Widget CreateContentWidget(object item)
    {
        var content = (AggregatedContentEntry)item;
        var latest = content.Versions.FirstOrDefault();
        var widget = (ContainerWidget)LoadWidget(null,
            ContentManager.Get<XElement>("Widgets/OnlineContentItem"), null);
        widget.Children.Find<LabelWidget>("OnlineContentItem.Name")!.Text = content.Name;
        widget.Children.Find<LabelWidget>("OnlineContentItem.Details")!.Text = latest is null
            ? $"{content.Type} | {content.Identifier}"
            : $"{content.Type} | {content.Identifier} | {latest.Version} | " +
              DataSizeFormatter.Format(latest.PackageSize);
        widget.Children.Find<LabelWidget>("OnlineContentItem.Sources")!.Text = latest is null
            ? string.Empty
            : string.Join(", ", latest.Sources.Select(source => source.RepositoryName).Distinct());
        return widget;
    }

    private void Refresh(bool clearVersionCache = false)
    {
        CancelRequest();
        ClearSelectedContent();
        if (clearVersionCache)
        {
            _versionCache.Clear();
        }

        _pageIndex = 1;
        _moveToNextPageAfterLoad = false;
        _state.Reset();
        RefreshReferences();
        _contentList.ClearItems();
        LoadPage();
    }

    private void LoadPage()
    {
        if (_busy)
        {
            return;
        }

        var repositories = GetSelectedRepositories();
        if (repositories.Count == 0)
        {
            ApplyFilter();
            _statusLabel.Text = Text("NoRepositories");
            return;
        }

        var pageIndex = _state.NextPageIndex;
        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        _busy = true;
        _statusLabel.Text = Text("Loading");
        var context = ContentSourceContext.Persistent(repositories);
        var query = new ContentCatalogQuery(_typeFilter?.ToString(), _search, pageIndex);
        var service = new ContentCatalogService(SettingsManager.ContentClients);
        Task.Run(() => service.QueryAsync(context, query, cancellation.Token), cancellation.Token)
            .ContinueWith(task => Dispatcher.Dispatch(() => CompletePage(task, cancellation, pageIndex)));
    }

    private void CompletePage(Task<AggregatedContentPage> task, CancellationTokenSource cancellation, int pageIndex)
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
            _moveToNextPageAfterLoad = false;
            Log.Error($"Online content query failed: {task.Exception}");
            _statusLabel.Text = Text("LoadFailed");
            return;
        }

        _state.Append(task.Result, pageIndex);
        if (_moveToNextPageAfterLoad)
        {
            _moveToNextPageAfterLoad = false;
            if (GetFilteredEntries().Count > _pageIndex * _pageSize)
            {
                _pageIndex++;
            }
        }

        ApplyFilter();
        TryOpenPendingNavigation();
        if (_pendingNavigation is not null && _state.HasMore)
        {
            LoadPage();
        }
    }

    private void ApplyFilter()
    {
        var selected = _contentList.SelectedItem as AggregatedContentEntry;
        _updatingContentList = true;
        _contentList.ClearItems();
        var filteredEntries = GetFilteredEntries();
        var maximumPageIndex = Math.Max(1, (filteredEntries.Count + _pageSize - 1) / _pageSize);
        _pageIndex = Math.Min(_pageIndex, maximumPageIndex);
        var entries = filteredEntries.Skip((_pageIndex - 1) * _pageSize).Take(_pageSize).ToArray();
        foreach (var entry in entries)
        {
            _contentList.AddItem(entry);
            if (selected is not null && selected.Type == entry.Type &&
                string.Equals(selected.Identifier, entry.Identifier, StringComparison.Ordinal))
            {
                _contentList.SelectedItem = entry;
            }
        }

        _updatingContentList = false;
        if (_contentList.SelectedItem is null)
        {
            ClearSelectedContent();
        }

        _statusLabel.Text = _state.Failures.Count > 0
            ? string.Format(Text("PartialFailure"), _state.Failures.Count)
            : entries.Length == 0 ? Text("NoResults") : string.Empty;
    }

    private IReadOnlyList<AggregatedContentEntry> GetFilteredEntries()
    {
        return _state.Filter(_search, _typeFilter, _repositoryId, _statusFilter);
    }

    private bool IsActionEnabled(object item)
    {
        if (_busy || item is not CatalogAction action)
        {
            return false;
        }

        var entriesCount = GetFilteredEntries().Count;
        return action switch
        {
            CatalogAction.Download => GetSelectedVersion() is { IsCached: false },
            CatalogAction.PreviousPage => _pageIndex > 1,
            CatalogAction.NextPage => entriesCount > _pageIndex * _pageSize || _state.HasMore,
            CatalogAction.Refresh => true,
            _ => false
        };
    }

    private void ExecuteAction(object item)
    {
        if (item is not CatalogAction action || !IsActionEnabled(action))
        {
            return;
        }

        switch (action)
        {
            case CatalogAction.Download when GetSelectedVersion() is { } version:
                Download(version.Version);
                break;
            case CatalogAction.PreviousPage:
                _pageIndex--;
                ApplyFilter();
                break;
            case CatalogAction.NextPage when GetFilteredEntries().Count > _pageIndex * _pageSize:
                _pageIndex++;
                ApplyFilter();
                break;
            case CatalogAction.NextPage:
                _moveToNextPageAfterLoad = true;
                LoadPage();
                break;
            case CatalogAction.Refresh:
                Refresh(clearVersionCache: true);
                break;
        }
    }

    private void TryOpenPendingNavigation()
    {
        if (_pendingNavigation is null)
        {
            return;
        }

        var match = _state.Find(_pendingNavigation);
        if (match is not null)
        {
            var navigation = _pendingNavigation;
            _pendingNavigation = null;
            SelectContent(match.Value.Entry, navigation);
        }
        else if (!_state.HasMore)
        {
            _pendingNavigation = null;
            _statusLabel.Text = Text("ExactNotFound");
        }
    }

    private void ContentSelectionChanged()
    {
        if (_updatingContentList)
        {
            return;
        }

        if (_contentList.SelectedItem is AggregatedContentEntry entry)
        {
            SelectContent(entry);
        }
        else
        {
            ClearSelectedContent();
        }
    }

    private void SelectContent(AggregatedContentEntry entry, OnlineContentNavigation? navigation = null)
    {
        if (navigation is null && _selectedEntry is { } selectedEntry && selectedEntry.Type == entry.Type &&
            string.Equals(selectedEntry.Identifier, entry.Identifier, StringComparison.Ordinal))
        {
            return;
        }

        _selectedEntry = entry;
        _selectedHash = null;
        _versionNavigation = navigation;
        if (TryUseCachedVersions(entry))
        {
            return;
        }

        RefreshVersions();
    }

    private void ClearSelectedContent()
    {
        _selectedEntry = null;
        _selectedHash = null;
        _versionNavigation = null;
        _updatingVersions = true;
        _versionDrawer.ClearItems();
        _updatingVersions = false;
    }

    private void RefreshVersions()
    {
        if (_selectedEntry is null)
        {
            return;
        }

        CancelRequest();
        _busy = true;
        _statusLabel.Text = Text("LoadingVersions");
        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        var entry = _selectedEntry;
        var service = new ContentCatalogService(SettingsManager.ContentClients);
        Task.Run(() => service.QueryVersionsAsync(CreateContext(), entry, cancellation.Token), cancellation.Token)
            .ContinueWith(task => Dispatcher.Dispatch(() => CompleteVersionRefresh(task, cancellation)));
    }

    private void CompleteVersionRefresh(Task<AggregatedContentDetails> task, CancellationTokenSource cancellation)
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
            _statusLabel.Text = Text("VersionLoadFailed");
            PopulateVersions();
            return;
        }

        _selectedEntry = task.Result.Entry;
        if (task.Result.Failures.Count == 0)
        {
            CacheVersions(task.Result.Entry);
        }

        _statusLabel.Text = task.Result.Failures.Count > 0
            ? string.Format(Text("PartialFailure"), task.Result.Failures.Count)
            : string.Empty;
        PopulateVersions();
    }

    private void PopulateVersions()
    {
        if (_selectedEntry is null)
        {
            return;
        }

        RefreshReferences();
        var versions = _state.GetVersions(_selectedEntry);
        _updatingVersions = true;
        _versionDrawer.SetItems(versions);
        var exactNavigation = _versionNavigation?.Normalize();
        var requestedHash = exactNavigation?.PackageHash ?? _selectedHash;
        _versionNavigation = null;
        var selectedVersion = versions.FirstOrDefault(version =>
            string.Equals(version.Version.PackageHash, requestedHash, StringComparison.Ordinal) &&
            (exactNavigation?.Version is null ||
             string.Equals(version.Version.Version, exactNavigation.Version, StringComparison.Ordinal)));
        _versionDrawer.SelectedItem = selectedVersion ?? versions.FirstOrDefault();
        _selectedHash = (_versionDrawer.SelectedItem as OnlineContentVersionState)?.Version.PackageHash;
        _updatingVersions = false;
        if (versions.Count == 0)
        {
            _statusLabel.Text = Text("NoVersions");
        }
        else if (exactNavigation?.PackageHash is not null && selectedVersion is null)
        {
            _statusLabel.Text = Text("ExactNotFound");
        }
    }

    private void VersionSelectionChanged()
    {
        if (_updatingVersions)
        {
            return;
        }

        _selectedHash = GetSelectedVersion()?.Version.PackageHash;
    }

    private OnlineContentVersionState? GetSelectedVersion()
    {
        return _versionDrawer.SelectedItem as OnlineContentVersionState;
    }

    private void Download(AggregatedContentVersion version)
    {
        if (_selectedEntry is null)
        {
            return;
        }

        CancelRequest();
        _busy = true;
        _statusLabel.Text = Text("Downloading");
        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        var cache = new ContentPackageCache(Storage.GetSystemPath(GamePaths.ContentPackageCache));
        var service = new ContentDownloadService(SettingsManager.ContentClients, cache);
        var entry = _selectedEntry;
        Task.Run(() => service.DownloadAsync(CreateContext(), entry, version, null, cancellation.Token),
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

    private static ContentSourceContext CreateContext()
    {
        return ContentSourceContext.Persistent(SettingsManager.ContentRepositories.Snapshot());
    }

    private bool TryUseCachedVersions(AggregatedContentEntry entry)
    {
        var key = (entry.Type, entry.Identifier);
        if (!_versionCache.TryGetValue(key, out var cachedVersions))
        {
            return false;
        }

        if (cachedVersions.ExpiresAt <= DateTime.UtcNow)
        {
            _versionCache.Remove(key);
            return false;
        }

        _selectedEntry = cachedVersions.Entry;
        _statusLabel.Text = string.Empty;
        PopulateVersions();
        return true;
    }

    private void CacheVersions(AggregatedContentEntry entry)
    {
        var now = DateTime.UtcNow;
        foreach (var key in _versionCache.Where(pair => pair.Value.ExpiresAt <= now)
                     .Select(pair => pair.Key).ToArray())
        {
            _versionCache.Remove(key);
        }

        var cacheKey = (entry.Type, entry.Identifier);
        if (_versionCache.Count >= _maximumVersionCacheEntries && !_versionCache.ContainsKey(cacheKey))
        {
            var oldestKey = _versionCache.MinBy(pair => pair.Value.ExpiresAt).Key;
            _versionCache.Remove(oldestKey);
        }

        _versionCache[cacheKey] = new CachedVersions(entry, now + _versionCacheDuration);
    }

    private void ApplySearchText()
    {
        var search = string.IsNullOrWhiteSpace(_searchTextBox.Text) ? null : _searchTextBox.Text.Trim();
        if (string.Equals(search, _search, StringComparison.Ordinal))
        {
            return;
        }

        _search = search;
        _searchTextBox.Text = search ?? string.Empty;
        Refresh();
    }

    private void RefreshFilterDrawers()
    {
        _updatingFilters = true;
        var typeOptions = new[] { new ContentTypeOption(null) }.Concat(Enum.GetValues<ContentPackageType>()
            .Select(type => new ContentTypeOption(type))).ToArray();
        _typeFilterDrawer.SetItems(typeOptions);
        _typeFilterDrawer.SelectedItem = typeOptions.First(option => option.Type == _typeFilter);
        var repositories = SettingsManager.ContentRepositories.Snapshot()
            .Where(repository => repository.IsEnabled).ToArray();
        var repositoryOptions = new[] { new RepositoryOption(null, Text("AllRepositories")) }.Concat(
            repositories.Select(repository =>
            new RepositoryOption(repository.Id, repository.Name))).ToArray();
        _repositoryFilterDrawer.SetItems(repositoryOptions);
        _repositoryFilterDrawer.SelectedItem = repositoryOptions.FirstOrDefault(option => option.Id == _repositoryId)
            ?? repositoryOptions[0];
        _repositoryId = ((RepositoryOption)_repositoryFilterDrawer.SelectedItem!).Id;
        var statusOptions = Enum.GetValues<OnlineContentStatusFilter>();
        _statusFilterDrawer.SetItems(statusOptions.Cast<object>());
        _statusFilterDrawer.SelectedItem = _statusFilter;
        _updatingFilters = false;
    }

    private void TypeFilterChanged()
    {
        if (_updatingFilters || _typeFilterDrawer.SelectedItem is not ContentTypeOption option)
        {
            return;
        }

        _typeFilter = option.Type;
        Refresh();
    }

    private void RepositoryFilterChanged()
    {
        if (_updatingFilters || _repositoryFilterDrawer.SelectedItem is not RepositoryOption option)
        {
            return;
        }

        _repositoryId = option.Id;
        Refresh();
    }

    private void StatusFilterChanged()
    {
        if (_updatingFilters || _statusFilterDrawer.SelectedItem is not OnlineContentStatusFilter status)
        {
            return;
        }

        _statusFilter = status;
        ApplyFilter();
    }

    private void UpdateSearchPlaceholder()
    {
        _searchPlaceholder.IsVisible = string.IsNullOrEmpty(_searchTextBox.Text);
    }

    private IReadOnlyList<ContentRepository> GetSelectedRepositories()
    {
        return SettingsManager.ContentRepositories.Snapshot()
            .Where(repository => repository.IsEnabled &&
                                 (_repositoryId is null || repository.Id == _repositoryId))
            .ToArray();
    }

    private void RefreshReferences()
    {
        var cache = new ContentPackageCache(Storage.GetSystemPath(GamePaths.ContentPackageCache));
        cache.RebuildIndex();
        var cached = cache.List();
        _state.SetReferences(cached.Select(entry => entry.PackageHash),
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
        return LanguageManager.GetContentWidgets(nameof(OnlineContentScreen), key);
    }

    private sealed record ContentTypeOption(ContentPackageType? Type);

    private sealed record CachedVersions(AggregatedContentEntry Entry, DateTime ExpiresAt);

    private sealed record RepositoryOption(Guid? Id, string Name);
}
