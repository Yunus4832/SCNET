using System.Xml.Linq;

using Content.Packaging;

using Game.Content;
using Game.Modding;

namespace Game.Screens;

public sealed class OnlineContentScreen : Screen
{
    private readonly ButtonWidget _loadMoreButton;
    private readonly ListPanelWidget _contentList;
    private readonly ButtonWidget _refreshButton;
    private readonly ButtonWidget _repositoryFilterButton;
    private readonly ButtonWidget _repositoriesButton;
    private readonly ButtonWidget _searchButton;
    private readonly LabelWidget _statusLabel;
    private readonly ButtonWidget _statusFilterButton;
    private readonly ButtonWidget _typeFilterButton;
    private readonly OnlineContentCatalogState _state = new();
    private bool _busy;
    private CancellationTokenSource? _cancellation;
    private OnlineContentNavigation? _pendingNavigation;
    private Guid? _repositoryId;
    private string? _search;
    private OnlineContentStatusFilter _statusFilter;
    private ContentPackageType? _typeFilter;

    public OnlineContentScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/OnlineContentScreen"));
        _contentList = Children.Find<ListPanelWidget>("ContentList")!;
        _searchButton = Children.Find<ButtonWidget>("Search")!;
        _typeFilterButton = Children.Find<ButtonWidget>("TypeFilter")!;
        _repositoryFilterButton = Children.Find<ButtonWidget>("RepositoryFilter")!;
        _statusFilterButton = Children.Find<ButtonWidget>("StatusFilter")!;
        _repositoriesButton = Children.Find<ButtonWidget>("Repositories")!;
        _refreshButton = Children.Find<ButtonWidget>("Refresh")!;
        _loadMoreButton = Children.Find<ButtonWidget>("LoadMore")!;
        _statusLabel = Children.Find<LabelWidget>("Status")!;
        _contentList.ItemWidgetFactory = CreateContentWidget;
        _contentList.ItemClicked += item => OpenDetails((AggregatedContentEntry)item);
    }

    public override void Enter(object[] parameters)
    {
        _pendingNavigation = parameters.FirstOrDefault() as OnlineContentNavigation;
        if (_pendingNavigation is not null)
        {
            _pendingNavigation = _pendingNavigation.Normalize();
            _typeFilter = _pendingNavigation.Type;
            _search = _pendingNavigation.Identifier;
            _repositoryId = null;
            _statusFilter = OnlineContentStatusFilter.All;
        }

        Refresh();
    }

    public override void Leave()
    {
        CancelRequest();
        _busy = false;
    }

    public override void Update()
    {
        UpdateButtonText();
        _searchButton.IsEnabled = !_busy;
        _typeFilterButton.IsEnabled = !_busy;
        _repositoryFilterButton.IsEnabled = !_busy;
        _statusFilterButton.IsEnabled = !_busy;
        _repositoriesButton.IsEnabled = !_busy;
        _refreshButton.IsEnabled = !_busy;
        _loadMoreButton.IsEnabled = !_busy && _state.HasMore;

        if (_searchButton.IsClicked)
        {
            ShowSearch();
        }

        if (_typeFilterButton.IsClicked)
        {
            ShowTypeFilter();
        }

        if (_repositoryFilterButton.IsClicked)
        {
            ShowRepositoryFilter();
        }

        if (_statusFilterButton.IsClicked)
        {
            ShowStatusFilter();
        }

        if (_repositoriesButton.IsClicked)
        {
            ScreensManager.SwitchScreen("ContentRepositories", "OnlineContent");
        }

        if (_refreshButton.IsClicked)
        {
            Refresh();
        }

        if (_loadMoreButton.IsClicked)
        {
            LoadPage();
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Content");
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

    private void Refresh()
    {
        CancelRequest();
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
            _statusLabel.Text = _contentList.Items.Count > 0 ? Text("OfflineCacheOnly") : Text("NoRepositories");
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
            Log.Error($"Online content query failed: {task.Exception}");
            _statusLabel.Text = Text("LoadFailed");
            return;
        }

        _state.Append(task.Result, pageIndex);
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
        _contentList.ClearItems();
        var entries = _state.Filter(_search, _typeFilter, _repositoryId, _statusFilter);
        foreach (var entry in entries)
        {
            _contentList.AddItem(entry);
            if (selected is not null && selected.Type == entry.Type &&
                string.Equals(selected.Identifier, entry.Identifier, StringComparison.Ordinal))
            {
                _contentList.SelectedItem = entry;
            }
        }

        _statusLabel.Text = _state.Failures.Count > 0
            ? string.Format(Text("PartialFailure"), _state.Failures.Count)
            : entries.Count == 0 ? Text("NoResults") : string.Empty;
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
            OpenDetails(match.Value.Entry, navigation);
        }
        else if (!_state.HasMore)
        {
            _pendingNavigation = null;
            _statusLabel.Text = Text("ExactNotFound");
        }
    }

    private void OpenDetails(AggregatedContentEntry entry, OnlineContentNavigation? navigation = null)
    {
        ScreensManager.SwitchScreen("OnlineContentVersions", entry, navigation ?? new OnlineContentNavigation());
    }

    private void ShowSearch()
    {
        DialogsManager.ShowDialog(null, new TextBoxDialog(Text("SearchTitle"), _search ?? string.Empty, 100,
            value =>
            {
                _search = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                Refresh();
            }, false));
    }

    private void ShowTypeFilter()
    {
        var options = new[] { new ContentTypeOption(null) }.Concat(Enum.GetValues<ContentPackageType>()
            .Select(type => new ContentTypeOption(type))).ToArray();
        DialogsManager.ShowDialog(null, new ListSelectionDialog(Text("TypeFilterTitle"), options, 56f,
            item => ((ContentTypeOption)item).Type?.ToString() ?? Text("All"),
            item =>
            {
                _typeFilter = ((ContentTypeOption)item).Type;
                Refresh();
            }));
    }

    private void ShowRepositoryFilter()
    {
        var repositories = SettingsManager.ContentRepositories.Snapshot()
            .Where(repository => repository.IsEnabled).ToArray();
        var options = new[] { new RepositoryOption(null, Text("All")) }.Concat(repositories.Select(repository =>
            new RepositoryOption(repository.Id, repository.Name))).ToArray();
        DialogsManager.ShowDialog(null, new ListSelectionDialog(Text("RepositoryFilterTitle"), options, 56f,
            item => ((RepositoryOption)item).Name,
            item =>
            {
                _repositoryId = ((RepositoryOption)item).Id;
                Refresh();
            }));
    }

    private void ShowStatusFilter()
    {
        var options = Enum.GetValues<OnlineContentStatusFilter>();
        DialogsManager.ShowDialog(null, new ListSelectionDialog(Text("StatusFilterTitle"), options, 56f,
            item => Text($"Status{item}"), item =>
            {
                _statusFilter = (OnlineContentStatusFilter)item;
                ApplyFilter();
            }));
    }

    private void UpdateButtonText()
    {
        _searchButton.Text = string.IsNullOrEmpty(_search)
            ? Text("Search")
            : string.Format(Text("SearchValue"), _search);
        _typeFilterButton.Text = _typeFilter is null ? Text("AllTypes") : _typeFilter.Value.ToString();
        _repositoryFilterButton.Text = _repositoryId is null
            ? Text("AllRepositories")
            : SettingsManager.ContentRepositories.Snapshot()
                .FirstOrDefault(repository => repository.Id == _repositoryId)?.Name ?? Text("AllRepositories");
        _statusFilterButton.Text = Text($"Status{_statusFilter}");
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
        _state.SeedCache(cached);
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

    private sealed record RepositoryOption(Guid? Id, string Name);
}
