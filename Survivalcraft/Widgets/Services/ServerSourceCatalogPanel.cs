using System.Xml.Linq;

using Game.Content;
using Game.Servers;

namespace Game.Widgets.Services;

public sealed class ServerSourceCatalogPanel : ServicePanelWidget
{
    private enum CatalogAction
    {
        Install = 0,

        Info = 1,

        Refresh = 2
    }

    private sealed record RepositoryFilterOption(Guid? RepositoryId, string Name);

    private readonly ActionPanelWidget _actionPanel;

    private readonly SelectionDrawerWidget _repositoryDrawer;

    private readonly TextBoxWidget _searchTextBox;

    private readonly LabelWidget _searchPlaceholder;

    private readonly ListPanelWidget _sourceList;

    private readonly LabelWidget _statusLabel;

    private IReadOnlyList<RegisteredServerSource> _sources = [];

    private CancellationTokenSource? _loadCancellation;

    private bool _busy;

    public ServerSourceCatalogPanel()
    {
        LoadContents(this, ContentManager.Get<XElement>("Widgets/Services/ServerSourceCatalogPanel"));
        _searchTextBox = Children.Find<TextBoxWidget>("Search")!;
        _searchPlaceholder = Children.Find<LabelWidget>("SearchPlaceholder")!;
        _repositoryDrawer = Children.Find<SelectionDrawerWidget>("RepositoryFilter")!;
        _sourceList = Children.Find<ListPanelWidget>("SourceList")!;
        _statusLabel = Children.Find<LabelWidget>("Status")!;
        _actionPanel = Children.Find<ActionPanelWidget>("Actions")!;
        _sourceList.ItemWidgetFactory = CreateSourceWidget;
        _searchTextBox.MaximumLength = 100;
        _searchTextBox.TextChanged += _ =>
        {
            UpdateSearchPlaceholder();
            ApplySearch();
        };
        _repositoryDrawer.ItemTextProvider = item => ((RepositoryFilterOption)item).Name;
        _repositoryDrawer.SelectionChanged += () => ApplySearch();
        _actionPanel.ItemTextProvider = item => Text(item.ToString()!);
        _actionPanel.ItemEnabledProvider = item => !_busy &&
                                                   (item is CatalogAction.Refresh ||
                                                    _sourceList.SelectedItem is RegisteredServerSource source &&
                                                    (item is CatalogAction.Info || !IsInstalled(source)));
        _actionPanel.ItemColorProvider = item => item is CatalogAction.Install ? new Color(50, 150, 35) : null;
        _actionPanel.ItemClicked += item =>
        {
            if (item is CatalogAction.Refresh) { LoadSources(); }
            else if (item is CatalogAction.Info && _sourceList.SelectedItem is RegisteredServerSource source)
            {
                DialogsManager.ShowDialog(
                    null,
                    new MessageDialog(
                        Text("Info"),
                        source.Name + "\n" + source.ApiUrl + "\n" + source.RepositoryName + "\n" + source.Description,
                        LanguageManager.Ok
                    )
                );
            }
            else { Install(); }
        };
        _actionPanel.SetPrimaryItems(
            [CatalogAction.Install, CatalogAction.Info, CatalogAction.Refresh],
            [1f, 1f, 1f, 0f]
        );
    }

    public override void Activate()
    {
        _searchPlaceholder.Text = Text("SearchPlaceholder");
        _searchTextBox.Text = string.Empty;
        UpdateSearchPlaceholder();
        RefreshRepositoryFilter();
        LoadSources();
    }

    public override void Deactivate()
    {
        _loadCancellation?.Cancel();
        _loadCancellation = null;
        _busy = false;
        _sources = [];
        _repositoryDrawer.Close();
        _sourceList.SelectedItem = null;
    }

    public override void Update()
    {
        _actionPanel.Refresh();
    }

    private static Widget CreateSourceWidget(object item)
    {
        var source = (RegisteredServerSource)item;
        var widget = (ContainerWidget)LoadWidget(null, ContentManager.Get<XElement>("Widgets/ServerSourceItem"), null);
        widget.Children.Find<LabelWidget>("ServerSourceItem.Name")!.Text = source.Name;
        widget.Children.Find<LabelWidget>("ServerSourceItem.Address")!.Text = source.ApiUrl;
        widget.Children.Find<LabelWidget>("ServerSourceItem.State")!.Text = IsInstalled(source)
            ? Text("Installed")
            : source.RepositoryName;
        return widget;
    }

    private void LoadSources()
    {
        _loadCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        _busy = true;
        _sources = [];
        _sourceList.ClearItems();
        _statusLabel.Text = Text("Loading");
        var catalog = new ContentServerSourceCatalog(ContentServerClientPool.Shared);
        Task.Run(
                () => catalog.QueryAsync(ContentRepositoryManager.Current.Snapshot(), cancellation.Token),
                cancellation.Token
            )
            .ContinueWith(task => Dispatcher.Dispatch(() => CompleteLoad(cancellation, task)));
    }

    private void CompleteLoad(
        CancellationTokenSource cancellation,
        Task<IReadOnlyList<RegisteredServerSource>> task
    )
    {
        if (!ReferenceEquals(_loadCancellation, cancellation))
        {
            _ = task.Exception;
            cancellation.Dispose();
            return;
        }

        _loadCancellation = null;
        cancellation.Dispose();
        _busy = false;
        if (task.IsCanceled)
        {
            return;
        }

        if (!task.IsCompletedSuccessfully)
        {
            Log.Error($"Server source catalog loading failed: {task.Exception}");
            _statusLabel.Text = Text("LoadFailed");
            return;
        }

        _sources = task.Result.OrderBy(source => source.RepositoryName).ThenBy(source => source.Name).ToArray();
        RefreshRepositoryFilter();
        ApplySearch();
    }

    private void Install()
    {
        if (_sourceList.SelectedItem is not RegisteredServerSource source || IsInstalled(source))
        {
            return;
        }

        try
        {
            ServerDirectoryManager.Current.InstallSource(
                new InstalledServerSource
                {
                    RegistrationId = $"{source.RepositoryId:N}:{source.RegistrationId}",
                    Name = source.Name,
                    ApiUrl = source.ApiUrl
                }
            );
            ApplySearch(source);
            _actionPanel.Refresh();
        }
        catch (Exception exception)
        {
            Log.Error($"Server source installation failed: {exception}");
            DialogsManager.Alert(Text("LoadFailed"));
        }
    }

    private static bool IsInstalled(RegisteredServerSource source)
    {
        var normalizedUrl = new Uri(source.ApiUrl).AbsoluteUri;
        return ServerDirectoryManager.Current.Snapshot().InstalledSources.Any(installedSource =>
            installedSource.ApiUrl == normalizedUrl
        );
    }

    private void ApplySearch(RegisteredServerSource? selected = null)
    {
        var selectedSource = selected ?? _sourceList.SelectedItem as RegisteredServerSource;
        var search = _searchTextBox.Text.Trim();
        var repositoryId = (_repositoryDrawer.SelectedItem as RepositoryFilterOption)?.RepositoryId;
        var visibleSources = _sources
            .Where(source => repositoryId is null || source.RepositoryId == repositoryId)
            .Where(source => search.Length == 0 || MatchesSearch(source, search)).ToArray();
        _sourceList.ClearItems();
        foreach (var source in visibleSources)
        {
            _sourceList.AddItem(source);
        }

        _sourceList.SelectedItem = selectedSource is null
            ? null
            : visibleSources.FirstOrDefault(source => source == selectedSource);
        _statusLabel.Text = !_busy && visibleSources.Length == 0 ? Text("Empty") : string.Empty;
        _actionPanel.Refresh();
    }

    private static bool MatchesSearch(RegisteredServerSource source, string search)
    {
        return source.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
               source.RepositoryName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
               source.ApiUrl.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               source.Description?.Contains(search, StringComparison.CurrentCultureIgnoreCase) == true;
    }

    private void UpdateSearchPlaceholder()
    {
        _searchPlaceholder.IsVisible = string.IsNullOrEmpty(_searchTextBox.Text);
    }

    private void RefreshRepositoryFilter()
    {
        var selectedId = (_repositoryDrawer.SelectedItem as RepositoryFilterOption)?.RepositoryId;
        var options = new[] { new RepositoryFilterOption(null, Text("AllRepositories")) }
            .Concat(
                ContentRepositoryManager.Current.Snapshot()
                    .Where(repository => repository.IsEnabled)
                    .OrderBy(repository => repository.Priority)
                    .Select(repository => new RepositoryFilterOption(repository.Id, repository.Name))
            ).ToArray();
        _repositoryDrawer.SetItems(options);
        _repositoryDrawer.SelectedItem = options.FirstOrDefault(option => option.RepositoryId == selectedId) ??
                                         options[0];
    }

    private static string Text(string key)
    {
        return LanguageManager.GetContentWidgets(nameof(ServerSourceCatalogPanel), key);
    }
}
