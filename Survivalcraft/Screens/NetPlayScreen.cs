using System.Net;
using System.Xml.Linq;

using Game.Content;
using Game.Network;
using Game.Servers;

namespace Game.Screens;

public sealed class NetPlayScreen : Screen
{
    private enum ServerAction
    {
        Connect,
        Add,
        Favorite,
        Refresh,
        Delete,
    }

    private enum ServerSortOrder
    {
        None,
        Ping,
        Players,
        Name
    }

    private enum SourceCategory
    {
        Local,
        External
    }

    private sealed record SourceFilterOption(IServerSource? Source);

    private sealed record ServerLoadResult(IReadOnlyList<ServerItem> Items, bool HadSourceErrors);

    private readonly ActionPanelWidget _actionPanel;
    private readonly ServerDiscoveryService _discoveryService = new();
    private readonly TextBoxWidget _searchTextBox;
    private readonly LabelWidget _searchPlaceholder;
    private readonly ListPanelWidget _serverList;
    private readonly SelectionDrawerWidget _sourceCategoryDrawer;
    private readonly SelectionDrawerWidget _sourceDrawer;
    private readonly SelectionDrawerWidget _sortDrawer;
    private readonly LabelWidget _statusLabel;
    private CancellationTokenSource? _refreshCancellation;
    private IReadOnlyList<ServerItem> _loadedServers = [];
    private IReadOnlyList<IServerSource> _sources = [];
    private bool _busy;
    private bool _hadSourceErrors;
    private bool _updatingSourceOptions;

    public NetPlayScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/NetPlayScreen"));
        _serverList = Children.Find<ListPanelWidget>("ServerList")!;
        _actionPanel = Children.Find<ActionPanelWidget>("Actions")!;
        _searchTextBox = Children.Find<TextBoxWidget>("Search")!;
        _searchPlaceholder = Children.Find<LabelWidget>("SearchPlaceholder")!;
        _sourceCategoryDrawer = Children.Find<SelectionDrawerWidget>("SourceCategory")!;
        _sourceDrawer = Children.Find<SelectionDrawerWidget>("SourceFilter")!;
        _sortDrawer = Children.Find<SelectionDrawerWidget>("SortOrder")!;
        _statusLabel = Children.Find<LabelWidget>("Status")!;
        _serverList.ItemWidgetFactory = CreateServerWidget;
        _serverList.ItemClicked += item =>
        {
            if (ReferenceEquals(item, _serverList.SelectedItem))
            {
                ConnectSelected();
            }
        };

        _searchPlaceholder.Text = Text("SearchPlaceholder");
        _searchTextBox.MaximumLength = 100;
        _searchTextBox.TextChanged += _ =>
        {
            UpdateSearchPlaceholder();
            ApplyView();
        };
        _sourceCategoryDrawer.ItemTextProvider = item => Text($"Category{item}");
        _sourceCategoryDrawer.SetItems(Enum.GetValues<SourceCategory>().Cast<object>());
        _sourceCategoryDrawer.SelectedItem = SourceCategory.Local;
        _sourceCategoryDrawer.SelectionChanged += ReloadSourceOptionsAndRefresh;
        _sourceDrawer.ItemTextProvider = item => GetSourceName((SourceFilterOption)item);
        _sourceDrawer.SelectionChanged += () =>
        {
            if (!_updatingSourceOptions)
            {
                RefreshSelectedSource();
            }
        };
        _sortDrawer.ItemTextProvider = item => Text($"Sort{item}");
        _sortDrawer.SelectionChanged += ApplyView;
        _sortDrawer.SetItems(Enum.GetValues<ServerSortOrder>().Cast<object>());
        _sortDrawer.SelectedItem = ServerSortOrder.None;
        _actionPanel.ItemTextProvider = GetActionText;
        _actionPanel.ItemEnabledProvider = IsActionEnabled;
        _actionPanel.ItemColorProvider = GetActionColor;
        _actionPanel.ItemClicked += ExecuteAction;
        _actionPanel.SetPrimaryItems(
            [ServerAction.Connect, ServerAction.Add, ServerAction.Favorite, ServerAction.Refresh],
            [3f, 2f, 2f, 2f]);
        _actionPanel.SetSecondaryItems([ServerAction.Delete]);

        GameEntry.HandleUri += uri =>
        {
            if (uri.Uri.Host != "online")
            {
                return;
            }

            var address = uri.Uri.AbsolutePath.TrimStart('/');
            if (!string.IsNullOrWhiteSpace(address))
            {
                ConnectToAddress(address, address, null);
            }

            uri.IsHandle = true;
        };
    }

    public override void Enter(object[] parameters)
    {
        _searchPlaceholder.Text = Text("SearchPlaceholder");
        _searchTextBox.Text = string.Empty;
        UpdateSearchPlaceholder();
        _sourceCategoryDrawer.RefreshItems();
        _sortDrawer.RefreshItems();
        _updatingSourceOptions = true;
        _sourceCategoryDrawer.SelectedItem = SourceCategory.Local;
        _updatingSourceOptions = false;
        ReloadSources();
    }

    public override void Leave()
    {
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = null;
        _busy = false;
        _sourceCategoryDrawer.Close();
        _sourceDrawer.Close();
        _sortDrawer.Close();
        _actionPanel.ShowPrimaryItems();
        _loadedServers = [];
        _hadSourceErrors = false;
        _serverList.SelectedItem = null;
    }

    public override void Update()
    {
        _actionPanel.Refresh();
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("MainMenu");
        }
    }

    private static Widget CreateServerWidget(object item)
    {
        var server = (ServerItem)item;
        var panel = new StackPanelWidget
        {
            Direction = LayoutDirection.Vertical,
            Margin = new Vector2(12f, 0f),
            VerticalAlignment = WidgetAlignment.Center
        };
        var title = new LabelWidget { FontScale = 0.8f, Ellipsis = true, MaxLines = 1 };
        var details = new LabelWidget
        {
            FontScale = 0.55f,
            Color = new Color(170, 170, 170),
            Ellipsis = true,
            MaxLines = 1
        };
        var sourceDetails = $"{Text("Source")}: {GetSourceName(server.SourceKind, server.SourceName)}";
        switch (server.RuntimeStatus.Availability)
        {
            case ServerAvailability.Available:
                title.Text = $"{server.DisplayName} ({server.RuntimeStatus.PingMilliseconds} ms)";
                title.Color = Color.LightGreen;
                details.Text = $"{sourceDetails} | {BuildDetails(server.RuntimeStatus)}";
                break;
            case ServerAvailability.Checking:
                title.Text = $"{server.DisplayName} {Text("Loading")}";
                details.Text = sourceDetails;
                break;
            case ServerAvailability.Unavailable:
                title.Text = $"{server.DisplayName} {Text("Unavailable")}";
                title.Color = Color.LightRed;
                details.Text = $"{sourceDetails} | {server.Address}";
                break;
            default:
                title.Text = server.DisplayName;
                details.Text = $"{sourceDetails} | {server.Address}";
                break;
        }

        panel.Children.Add(title);
        panel.Children.Add(details);
        return panel;
    }

    private static string BuildDetails(ServerRuntimeStatus status)
    {
        return $"{status.Version} | {Text("Players")}: " +
               $"{status.PlayerCount}/{status.MaxPlayerCount} | {Text("Mode")}: " +
               $"{LanguageManager.Get("GameMode", status.GameMode.ToString())} | " +
               $"{Text("Time")}: " +
               $"{SubsystemTimeOfDay.GetTimeOfDayText(status.TimeOfDay)} | " +
               $"{Text("Season")}: {GetSeasonText(status.Season, status.TimeOfSeason)}";
    }

    private static string GetSeasonText(Season season, float timeOfSeason)
    {
        var index = season switch
        {
            Season.Summer => timeOfSeason < 0.33f ? 0 : timeOfSeason < 0.67f ? 1 : 2,
            Season.Autumn => timeOfSeason < 0.33f ? 3 : timeOfSeason < 0.67f ? 4 : 5,
            Season.Winter => timeOfSeason < 0.33f ? 6 : timeOfSeason < 0.67f ? 7 : 8,
            Season.Spring => timeOfSeason < 0.33f ? 9 : timeOfSeason < 0.67f ? 10 : 11,
            _ => 1
        };
        return LanguageManager.Get("SubsystemSeasons", index);
    }

    private void ReloadSources()
    {
        _sources = SettingsManager.ServerSources.GetEnabledSources();
        ReloadSourceOptions();
        RefreshSelectedSource();
    }

    private void ReloadSourceOptionsAndRefresh()
    {
        if (_updatingSourceOptions)
        {
            return;
        }

        ReloadSourceOptions();
        RefreshSelectedSource();
    }

    private void RefreshSelectedSource()
    {
        if (_sourceDrawer.SelectedItem is not SourceFilterOption option)
        {
            return;
        }

        var sources = option.Source is null ? GetCategorySources() : [option.Source];

        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _refreshCancellation = cancellation;
        _busy = true;
        _statusLabel.Text = Text("Loading");
        _loadedServers = [];
        _hadSourceErrors = false;
        _serverList.ClearItems();
        _actionPanel.Refresh();
        Task.Run(() => LoadAndProbeAsync(sources, cancellation.Token), cancellation.Token)
            .ContinueWith(task => Dispatcher.Dispatch(() => CompleteRefresh(cancellation, task)));
    }

    private async Task<ServerLoadResult> LoadAndProbeAsync(IReadOnlyList<IServerSource> sources,
        CancellationToken cancellationToken)
    {
        var sourceResults = await Task.WhenAll(sources.Select(async source =>
        {
            try
            {
                return (Items: await source.LoadAsync(cancellationToken).ConfigureAwait(false), Failed: false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Log.Warning($"Could not load server source '{source.Name}': {exception.Message}");
                return (Items: [], Failed: true);
            }
        })).ConfigureAwait(false);
        var items = sourceResults.SelectMany(result => result.Items).ToArray();

        using var concurrency = new SemaphoreSlim(8);
        await Task.WhenAll(items.Where(item => item.SourceKind != ServerSourceKind.Lan).Select(async item =>
        {
            await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                item.RuntimeStatus = new ServerRuntimeStatus { Availability = ServerAvailability.Checking };
                item.RuntimeStatus = await _discoveryService.ProbeAsync(item.Address, TimeSpan.FromMilliseconds(800),
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                concurrency.Release();
            }
        })).ConfigureAwait(false);
        return new ServerLoadResult(items, sourceResults.Any(result => result.Failed));
    }

    private void CompleteRefresh(CancellationTokenSource cancellation, Task<ServerLoadResult> task)
    {
        if (!ReferenceEquals(_refreshCancellation, cancellation))
        {
            cancellation.Dispose();
            return;
        }

        _refreshCancellation = null;
        cancellation.Dispose();
        _busy = false;
        if (task.IsCanceled)
        {
            return;
        }

        if (!task.IsCompletedSuccessfully)
        {
            Log.Error($"Server source loading failed: {task.Exception}");
            _statusLabel.Text = Text("LoadFailed");
            _actionPanel.Refresh();
            return;
        }

        _loadedServers = task.Result.Items;
        _hadSourceErrors = task.Result.HadSourceErrors;
        ApplyView();
        _actionPanel.Refresh();
    }

    private void ApplyView()
    {
        var selectedIdentity = (_serverList.SelectedItem as ServerItem)?.Identity;
        var search = _searchTextBox.Text.Trim();
        IEnumerable<ServerItem> servers = _loadedServers;
        if (search.Length > 0)
        {
            servers = servers.Where(server => MatchesSearch(server, search));
        }

        servers = (_sortDrawer.SelectedItem as ServerSortOrder?) switch
        {
            ServerSortOrder.Ping => servers
                .OrderBy(server => server.RuntimeStatus.Availability == ServerAvailability.Available ? 0 : 1)
                .ThenBy(server => server.RuntimeStatus.PingMilliseconds),
            ServerSortOrder.Players => servers
                .OrderBy(server => server.RuntimeStatus.Availability == ServerAvailability.Available ? 0 : 1)
                .ThenByDescending(server => server.RuntimeStatus.PlayerCount),
            ServerSortOrder.Name => servers.OrderBy(server => server.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            _ => servers
        };

        var visibleServers = servers.ToArray();
        _serverList.ClearItems();
        foreach (var server in visibleServers)
        {
            _serverList.AddItem(server);
        }

        _serverList.SelectedItem = selectedIdentity is null
            ? null
            : visibleServers.FirstOrDefault(server => server.Identity == selectedIdentity);
        _statusLabel.Text = _busy
            ? Text("Loading")
            : _hadSourceErrors
                ? _loadedServers.Count == 0 ? Text("LoadFailed") : Text("PartialLoadFailed")
                : visibleServers.Length == 0 ? Text("Empty") : string.Empty;
        _actionPanel.Refresh();
    }

    private static bool MatchesSearch(ServerItem server, string search)
    {
        return server.DisplayName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
               server.Address.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               GetSourceName(server.SourceKind, server.SourceName)
                   .Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
               server.Description.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
               server.Tags.Any(tag => tag.Contains(search, StringComparison.CurrentCultureIgnoreCase));
    }

    private void UpdateSearchPlaceholder()
    {
        _searchPlaceholder.IsVisible = string.IsNullOrEmpty(_searchTextBox.Text);
    }

    private bool IsActionEnabled(object item)
    {
        if (_busy || item is not ServerAction action)
        {
            return false;
        }

        var selected = _serverList.SelectedItem as ServerItem;
        return action switch
        {
            ServerAction.Connect => selected?.RuntimeStatus.Availability == ServerAvailability.Available,
            ServerAction.Add or ServerAction.Refresh => true,
            ServerAction.Favorite => selected is not null && selected.SourceKind != ServerSourceKind.Favorites,
            ServerAction.Delete => selected?.SourceKind is ServerSourceKind.MyServers or
                ServerSourceKind.Favorites or ServerSourceKind.Recent,
            _ => false
        };
    }

    private void ExecuteAction(object item)
    {
        if (item is not ServerAction action || !IsActionEnabled(action))
        {
            return;
        }

        var selected = _serverList.SelectedItem as ServerItem;
        switch (action)
        {
            case ServerAction.Connect:
                ConnectSelected();
                break;
            case ServerAction.Add:
                ShowAddDialog();
                break;
            case ServerAction.Refresh:
                RefreshSelectedSource();
                break;
            case ServerAction.Favorite when selected is not null:
                AddFavorite(selected);
                break;
            case ServerAction.Delete when selected is not null:
                ConfirmDelete(selected);
                break;
        }
    }

    private void ShowAddDialog()
    {
        DialogsManager.ShowDialog(null, new ContentRepositoryDialog(Text("AddTitle"), Text("NameLabel"),
            Text("AddressLabel"), Text("Add"), string.Empty, string.Empty, (name, address) =>
            {
                try
                {
                    SettingsManager.ServerDirectory.AddMyServer(name, address);
                    SelectSourceAndRefresh(ServerSourceIds.MyServers);
                    return true;
                }
                catch (ArgumentException)
                {
                    DialogsManager.Alert(Text("InvalidServer"));
                    return false;
                }
            }));
    }

    private void AddFavorite(ServerItem server)
    {
        try
        {
            SettingsManager.ServerDirectory.AddFavorite(server.DisplayName, server.Address);
        }
        catch (ArgumentException)
        {
            DialogsManager.Alert(Text("DuplicateFavorite"));
        }
    }

    private void ConfirmDelete(ServerItem server)
    {
        if (!Guid.TryParseExact(server.EntryId, "N", out var id))
        {
            return;
        }

        DialogsManager.Confirm(string.Format(Text("ConfirmDelete"), server.DisplayName), button =>
        {
            if (button == MessageDialogButton.Button1)
            {
                switch (server.SourceKind)
                {
                    case ServerSourceKind.MyServers:
                        SettingsManager.ServerDirectory.DeleteMyServer(id);
                        break;
                    case ServerSourceKind.Favorites:
                        SettingsManager.ServerDirectory.DeleteFavorite(id);
                        break;
                    case ServerSourceKind.Recent:
                        SettingsManager.ServerDirectory.DeleteRecentServer(id);
                        break;
                    default:
                        return;
                }

                RefreshSelectedSource();
            }
        });
    }

    private void ConnectSelected()
    {
        if (_serverList.SelectedItem is not ServerItem server ||
            server.RuntimeStatus.Availability != ServerAvailability.Available)
        {
            return;
        }

        ConnectToAddress(server.DisplayName, server.Address, server.RuntimeStatus);
    }

    public void ConnectToRemoteSession(IPEndPoint endPoint)
    {
        var busyDialog = new BusyDialog(Text("Connect"), Text("Loading"));
        DialogsManager.ShowDialog(null, busyDialog);
        var address = endPoint.ToString();
        Task.Run(() => _discoveryService.ProbeAsync(address, TimeSpan.FromSeconds(2), CancellationToken.None))
            .ContinueWith(task => Dispatcher.Dispatch(() =>
            {
                DialogsManager.HideDialog(busyDialog);
                if (!task.IsCompletedSuccessfully ||
                    task.Result.Availability != ServerAvailability.Available)
                {
                    DialogsManager.Alert(Text("LoadFailed"));
                    return;
                }

                ConnectToAddress(address, address, task.Result);
            }));
    }

    private void ConnectToAddress(string name, string address, ServerRuntimeStatus? status)
    {
        if (!CommonLib.Resolve(address, out var endpoint))
        {
            DialogsManager.Alert(LanguageManager.Get("Usual", "error"));
            return;
        }

        SettingsManager.ServerDirectory.RecordConnectionAttempt(name, address, DateTimeOffset.UtcNow);
        PrepareRemoteSessionAndConnect(endpoint!, status?.RequiredModProfile, status?.TemporaryRepositories ?? []);
    }

    private void SelectSourceAndRefresh(string sourceId)
    {
        _sourceCategoryDrawer.SelectedItem = SourceCategory.Local;
        _sourceDrawer.SelectedItem = _sourceDrawer.Items.Cast<SourceFilterOption>()
            .First(option => option.Source?.Id == sourceId);
    }

    private void ReloadSourceOptions()
    {
        var selectedId = (_sourceDrawer.SelectedItem as SourceFilterOption)?.Source?.Id;
        var options = new[] { new SourceFilterOption(null) }
            .Concat(GetCategorySources().Select(source => new SourceFilterOption(source))).ToArray();
        _updatingSourceOptions = true;
        try
        {
            _sourceDrawer.SetItems(options);
            _sourceDrawer.SelectedItem = options.FirstOrDefault(option => option.Source?.Id == selectedId) ??
                                         options[0];
            _sourceDrawer.IsEnabled = options.Length > 1;
        }
        finally
        {
            _updatingSourceOptions = false;
        }
    }

    private IReadOnlyList<IServerSource> GetCategorySources()
    {
        return _sourceCategoryDrawer.SelectedItem switch
        {
            SourceCategory.Local => _sources.Where(source => source.Kind != ServerSourceKind.Http).ToArray(),
            _ => _sources.Where(source => source.Kind == ServerSourceKind.Http).ToArray()
        };
    }

    private string GetActionText(object item)
    {
        if (item is not ServerAction action)
        {
            return string.Empty;
        }

        return Text(action.ToString());
    }

    private static Color? GetActionColor(object item)
    {
        return item switch
        {
            ServerAction.Connect => new Color(50, 150, 35),
            ServerAction.Delete => new Color(150, 50, 35),
            _ => null
        };
    }

    private string GetSourceName(SourceFilterOption option)
    {
        var source = option.Source;
        if (source is null)
        {
            return _sourceCategoryDrawer.SelectedItem switch
            {
                SourceCategory.Local => Text("AllLocalSources"),
                _ => Text("AllExternalSources")
            };
        }

        return GetSourceName(source.Kind, source.Name);
    }

    private static string GetSourceName(ServerSourceKind kind, string fallbackName)
    {
        return kind switch
        {
            ServerSourceKind.MyServers => Text("MyServers"),
            ServerSourceKind.Favorites => Text("Favorites"),
            ServerSourceKind.Recent => Text("Recent"),
            ServerSourceKind.Lan => Text("Lan"),
            _ => fallbackName
        };
    }

    private void PrepareRemoteSessionAndConnect(IPEndPoint endPoint, ModProfile? requiredProfile,
        IReadOnlyList<ContentRepository> temporaryRepositories)
    {
        if (requiredProfile is not { Packages.Count: > 0 })
        {
            ConnectPreparedRemoteSession(endPoint);
            return;
        }

        var busyDialog = new BusyDialog("准备服务器模组", "正在检查所需模组...");
        DialogsManager.ShowDialog(null, busyDialog);
        Task.Run(() => ModRestartHelper.PrepareRemoteSession(SessionInfoManager.CreateRemoteClientSession(endPoint),
                requiredProfile, temporaryRepositories,
                message => Dispatcher.Dispatch(() => busyDialog.SmallMessage = message)))
            .ContinueWith(task => Dispatcher.Dispatch(() =>
            {
                DialogsManager.HideDialog(busyDialog);
                if (!task.IsCompletedSuccessfully)
                {
                    Log.Error($"Remote mod session preparation failed: {task.Exception}");
                    DialogsManager.Alert(LanguageManager.Get("Usual", "error"));
                    return;
                }

                if (!task.Result.RequiresRestart)
                {
                    ConnectPreparedRemoteSession(endPoint);
                    return;
                }

                ConfirmRemoteModRestart(task.Result);
            }));
    }

    private static void ConnectPreparedRemoteSession(IPEndPoint endPoint)
    {
        DialogsManager.HideAllDialogs();
        ScreensManager.SwitchScreen("GameLoading", string.Empty, string.Empty, endPoint);
    }

    private static void ConfirmRemoteModRestart(RemoteModSessionPreparation result)
    {
        DialogsManager.ShowDialog(null, new MessageDialog("需要重启游戏",
            $"{result.RestartReason}\n\n是否现在重启？", "重启", "取消", button =>
            {
                if (button == MessageDialogButton.Button1)
                {
                    GameExitManager.RequestRestart(result.RemoteSession!, result.SessionProfile!);
                }
            }));
    }

    private static string Text(string key)
    {
        return LanguageManager.GetContentWidgets(nameof(NetPlayScreen), key);
    }
}
