using System.Xml.Linq;

using Engine.Network;

using Game.Content;
using Game.Servers;

namespace Game.Screens;

public sealed class RendezvousCatalogScreen : Screen
{
    private enum CatalogAction
    {
        Install
    }

    private sealed record RepositoryFilterOption(Guid? RepositoryId, string Name);

    private readonly ActionPanelWidget _actionPanel;
    private readonly SelectionDrawerWidget _repositoryDrawer;
    private readonly TextBoxWidget _searchTextBox;
    private readonly LabelWidget _searchPlaceholder;
    private readonly ListPanelWidget _sourceList;
    private readonly LabelWidget _statusLabel;
    private IReadOnlyList<RegisteredRendezvousService> _sources = [];
    private CancellationTokenSource? _loadCancellation;
    private bool _busy;
    private IReadOnlySet<Guid> _unavailableRepositories = new HashSet<Guid>();
    private CancellableBusyDialog? _busyDialog;

    public RendezvousCatalogScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/RendezvousCatalogScreen"));
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
        _actionPanel.ItemTextProvider = _ => Text("Install");
        _actionPanel.ItemEnabledProvider = _ => !_busy && _sourceList.SelectedItem is RegisteredRendezvousService source &&
                                                !IsInstalled(source);
        _actionPanel.ItemClicked += _ => Install();
        _actionPanel.SetPrimaryItems([CatalogAction.Install]);
    }

    public override void Enter(object[] parameters)
    {
        _searchPlaceholder.Text = Text("SearchPlaceholder");
        _searchTextBox.Text = string.Empty;
        UpdateSearchPlaceholder();
        RefreshRepositoryFilter();
        LoadSources();
    }

    public override void Leave()
    {
        _loadCancellation?.Cancel();
        _loadCancellation = null;
        _busy = false;
        if (_busyDialog is not null)
        {
            DialogsManager.HideDialog(_busyDialog);
            _busyDialog = null;
        }
        _sources = [];
        _repositoryDrawer.Close();
        _sourceList.SelectedItem = null;
    }

    public override void Update()
    {
        _actionPanel.Refresh();
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Content");
        }
    }

    private static Widget CreateSourceWidget(object item)
    {
        var source = (RegisteredRendezvousService)item;
        var widget = (ContainerWidget)LoadWidget(null, ContentManager.Get<XElement>("Widgets/ServerSourceItem"), null);
        widget.Children.Find<LabelWidget>("ServerSourceItem.Name")!.Text = source.Name;
        widget.Children.Find<LabelWidget>("ServerSourceItem.Address")!.Text = source.ControlUrl;
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
        _unavailableRepositories = new HashSet<Guid>();
        _sourceList.ClearItems();
        _statusLabel.Text = Text("Loading");
        var catalog = new ContentRendezvousCatalog(ContentServerClientPool.Shared);
        Task.Run(() => catalog.QueryAsync(ContentRepositoryManager.Current.Snapshot(), cancellation.Token),
                cancellation.Token)
            .ContinueWith(task => Dispatcher.Dispatch(() => CompleteLoad(cancellation, task)));
    }

    private void CompleteLoad(CancellationTokenSource cancellation,
        Task<RendezvousCatalogResult> task)
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

        _unavailableRepositories = task.Result.UnavailableRepositories;
        _sources = task.Result.Services.OrderBy(source => source.RepositoryName).ThenBy(source => source.Name).ToArray();
        RefreshRepositoryFilter();
        ApplySearch();
    }

    private void Install()
    {
        if (_sourceList.SelectedItem is not RegisteredRendezvousService source || IsInstalled(source))
        {
            return;
        }

        var dialog = new CancellableBusyDialog(Text("Testing"), true);
        var cancellation = dialog.Progress.CancellationTokenSource;
        _loadCancellation = cancellation;
        _busyDialog = dialog;
        _busy = true;
        DialogsManager.ShowDialog(null, dialog);
        Task.Run(() => RendezvousServiceProbe.TestAsync(source.ControlUrl, cancellation.Token), cancellation.Token)
            .ContinueWith(task => Dispatcher.Dispatch(() =>
            {
                if (!ReferenceEquals(_loadCancellation, cancellation))
                {
                    _ = task.Exception;
                    cancellation.Dispose();
                    return;
                }
                _loadCancellation = null;
                _busyDialog = null;
                _busy = false;
                DialogsManager.HideDialog(dialog);
                cancellation.Dispose();
                if (task.IsCanceled)
                {
                    return;
                }
                try
                {
                    if (!task.IsCompletedSuccessfully)
                    {
                        throw task.Exception!.GetBaseException();
                    }
                    RendezvousManager.Current.Add(new InstalledRendezvousService
                    {
                        RegistrationId = $"{source.RepositoryId:N}:{source.RegistrationId}",
                        Name = source.Name,
                        ControlUrl = source.ControlUrl,
                        Order = RendezvousManager.Current.Snapshot().Count
                    });
                    ApplySearch(source);
                    _actionPanel.Refresh();
                }
                catch (Exception exception)
                {
                    Log.Warning($"Signaling service installation failed: {exception.Message}");
                    DialogsManager.Alert(Text("LoadFailed"));
                }
            }));
    }

    private static bool IsInstalled(RegisteredRendezvousService source)
    {
        var normalizedUrl = RendezvousAddress.Normalize(new Uri(source.ControlUrl)).AbsoluteUri;
        return RendezvousManager.Current.Snapshot().Any(installedSource =>
            installedSource.ControlUrl == normalizedUrl);
    }

    private void ApplySearch(RegisteredRendezvousService? selected = null)
    {
        var selectedSource = selected ?? _sourceList.SelectedItem as RegisteredRendezvousService;
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
        var unavailable = repositoryId is { } id ? _unavailableRepositories.Contains(id) : _unavailableRepositories.Count > 0;
        _statusLabel.Text = !_busy && visibleSources.Length == 0 ? Text(unavailable ? "LoadFailed" : "Empty") : string.Empty;
        _actionPanel.Refresh();
    }

    private static bool MatchesSearch(RegisteredRendezvousService source, string search)
    {
        return source.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
               source.RepositoryName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
               source.ControlUrl.Contains(search, StringComparison.OrdinalIgnoreCase) ||
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
            .Concat(ContentRepositoryManager.Current.Snapshot()
                .Where(repository => repository.IsEnabled)
                .OrderBy(repository => repository.Priority)
                .Select(repository => new RepositoryFilterOption(repository.Id, repository.Name))).ToArray();
        _repositoryDrawer.SetItems(options);
        _repositoryDrawer.SelectedItem = options.FirstOrDefault(option => option.RepositoryId == selectedId) ??
                                         options[0];
    }

    private static string Text(string key)
    {
        return LanguageManager.GetContentWidgets(nameof(RendezvousCatalogScreen), key);
    }
}
