using System.Xml.Linq;

using Engine.Network;

using Game.Servers;

namespace Game.Screens;

public sealed class NetPlayScreen : Screen
{
    private enum ServerAction
    {
        Connect,
        JoinCode,
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
        Lan,
        External,
        Rendezvous
    }

    private sealed record SourceFilterOption(
        IServerSource? Source,
        LocalServerTag Tag = LocalServerTag.None,
        InstalledRendezvousService? Rendezvous = null);

    private sealed record ServerLoadResult(IReadOnlyList<ServerItem> Items, bool HadSourceErrors);

    private static readonly HttpClient _connectionHttp = new();

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
    private CancellableProgress? _connectionProgress;
    private IReadOnlyList<ServerItem> _loadedServers = [];
    private IReadOnlyList<RendezvousRoom> _loadedRooms = [];

    private bool IsRoomCategory => _sourceCategoryDrawer.SelectedItem is SourceCategory.Rendezvous;
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
        _actionPanel.SetSecondaryItems([ServerAction.JoinCode, ServerAction.Delete]);

        GameEntry.HandleUri += uri =>
        {
            if (uri.Uri.Host != "online")
            {
                return;
            }

            var address = uri.Uri.AbsolutePath.TrimStart('/');
            if (!string.IsNullOrWhiteSpace(address))
            {
                ConnectToAddress(address, address);
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
        ReloadSources();
    }

    public override void Leave()
    {
        _connectionProgress?.Cancel();
        _refreshCancellation?.Cancel();
        _refreshCancellation = null;
        _busy = false;
        _sourceCategoryDrawer.Close();
        _sourceDrawer.Close();
        _sortDrawer.Close();
        _actionPanel.ShowPrimaryItems();
        _loadedServers = [];
        _loadedRooms = [];
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
        if (item is RendezvousRoom room)
        {
            var row = new StackPanelWidget
            {
                Direction = LayoutDirection.Vertical,
                Margin = new Vector2(12f, 0f),
                VerticalAlignment = WidgetAlignment.Center
            };
            row.Children.Add(new LabelWidget
            { Text = room.Summary.Name, FontScale = 0.8f, Ellipsis = true, MaxLines = 1 });
            row.Children.Add(new LabelWidget
            {
                Text = $"{room.ServiceName} | {room.Summary.JoinCode} | " +
                       string.Join(" / ", room.Summary.ConnectionMethods.Select(method =>
                           Text(method == "holePunch" ? "RoomHolePunch" : "RoomRelay"))) +
                       $" | {room.Summary.Description}",
                FontScale = 0.55f,
                Color = new Color(170, 170, 170),
                Ellipsis = true,
                MaxLines = 1
            });
            return row;
        }

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
        var sourceName = GetSourceName(server.SourceKind, server.SourceName);
        if (server.SourceKind == ServerSourceKind.Local)
        {
            sourceName += " | " + string.Join(", ", GetLocalTagNames(server.LocalTags));
        }

        var sourceDetails = $"{Text("Source")}: {sourceName}";
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
        _sources = ServerSourceCatalog.Current.GetEnabledSources();
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

        if (IsRoomCategory)
        {
            RefreshRooms(option.Rendezvous);
            return;
        }

        _loadedRooms = [];
        var sources = option.Source is null ? GetCategorySources() : [option.Source];

        _refreshCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _refreshCancellation = cancellation;
        _busy = true;
        _statusLabel.Text = Text("Loading");
        _loadedServers = [];
        _hadSourceErrors = false;
        _serverList.ClearItems();
        _actionPanel.Refresh();
        Task.Run(() => LoadAndProbeAsync(sources, cancellation.Token))
            .ContinueWith(task => Dispatcher.Dispatch(() => CompleteRefresh(cancellation, task)));
    }

    private void RefreshRooms(InstalledRendezvousService? service)
    {
        _refreshCancellation?.Cancel();
        _loadedServers = [];
        _loadedRooms = [];
        _hadSourceErrors = false;
        _serverList.ClearItems();
        var services = service is null
            ? RendezvousManager.Current.Snapshot().Where(item => item.IsEnabled).ToArray()
            : [service];
        if (services.Length == 0)
        {
            _refreshCancellation = null;
            _busy = false;
            _statusLabel.Text = LanguageManager.GetContentWidgets("ManageRendezvousScreen", "NoSources");
            _actionPanel.Refresh();
            return;
        }

        var cancellation = new CancellationTokenSource();
        _refreshCancellation = cancellation;
        _busy = true;
        ApplyView();
        Task.Run(() => LoadRoomsAsync(services, cancellation.Token))
            .ContinueWith(task => Dispatcher.Dispatch(() =>
            {
                if (!ReferenceEquals(_refreshCancellation, cancellation))
                {
                    _ = task.Exception;
                    cancellation.Dispose();
                    return;
                }

                _refreshCancellation = null;
                var canceled = cancellation.IsCancellationRequested;
                cancellation.Dispose();
                _busy = false;
                if (canceled)
                {
                    _ = task.Exception;
                    return;
                }

                if (task.IsCompletedSuccessfully)
                {
                    _loadedRooms = task.Result.Rooms;
                    _hadSourceErrors = task.Result.HadErrors;
                }
                else
                {
                    _hadSourceErrors = true;
                    Log.Warning($"Could not load signaling rooms: {task.Exception?.GetBaseException().Message}");
                }

                ApplyView();
            }));
    }

    private async Task<(IReadOnlyList<RendezvousRoom> Rooms, bool HadErrors)> LoadRoomsAsync(
        IReadOnlyList<InstalledRendezvousService> services, CancellationToken cancellationToken)
    {
        using var concurrency = new SemaphoreSlim(8);
        var results = await Task.WhenAll(services.Select(async service =>
        {
            await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return (Rooms: await RendezvousRoomDirectory.LoadAsync(_connectionHttp, service, cancellationToken)
                    .ConfigureAwait(false), Failed: false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Log.Warning($"Could not load signaling rooms from '{service.Name}': {exception.Message}");
                return (Rooms: (IReadOnlyList<RendezvousRoom>)Array.Empty<RendezvousRoom>(), Failed: true);
            }
            finally
            {
                concurrency.Release();
            }
        })).ConfigureAwait(false);
        return (results.SelectMany(result => result.Rooms).ToArray(), results.Any(result => result.Failed));
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
            _ = task.Exception;
            cancellation.Dispose();
            return;
        }

        _refreshCancellation = null;
        var canceled = cancellation.IsCancellationRequested;
        cancellation.Dispose();
        _busy = false;
        if (task.IsCanceled || canceled)
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
        if (IsRoomCategory)
        {
            var selectedRoom = _serverList.SelectedItem as RendezvousRoom;
            var searchText = _searchTextBox.Text.Trim();
            var rooms = _loadedRooms.Where(room =>
                room.Summary.Name.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ||
                room.Summary.Description.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ||
                room.Summary.JoinCode.Contains(searchText, StringComparison.Ordinal));
            if (_sortDrawer.SelectedItem is ServerSortOrder.Name)
            {
                rooms = rooms.OrderBy(room => room.Summary.Name, StringComparer.CurrentCultureIgnoreCase);
            }

            var visible = rooms.ToArray();
            _serverList.ClearItems();
            foreach (var room in visible)
            {
                _serverList.AddItem(room);
            }

            _serverList.SelectedItem = visible.FirstOrDefault(room =>
                selectedRoom is not null && room.Summary.Id == selectedRoom.Summary.Id &&
                room.ControlUrl == selectedRoom.ControlUrl);
            _statusLabel.Text = _busy
                ? Text("Loading")
                : _hadSourceErrors
                    ? Text("LoadFailed")
                    : !RendezvousManager.Current.Snapshot().Any(service => service.IsEnabled)
                        ? LanguageManager.GetContentWidgets("ManageRendezvousScreen", "NoSources")
                        : visible.Length == 0
                            ? Text("Empty")
                            : string.Empty;
            _actionPanel.Refresh();
            return;
        }

        var selectedIdentity = (_serverList.SelectedItem as ServerItem)?.Identity;
        var search = _searchTextBox.Text.Trim();
        IEnumerable<ServerItem> servers = _loadedServers;
        if (_sourceCategoryDrawer.SelectedItem is SourceCategory.Local &&
            _sourceDrawer.SelectedItem is SourceFilterOption { Tag: not LocalServerTag.None } option)
        {
            servers = servers.Where(server => server.LocalTags.HasFlag(option.Tag));
            if (option.Tag == LocalServerTag.Recent &&
                _sortDrawer.SelectedItem is ServerSortOrder.None)
            {
                servers = servers.OrderByDescending(server => server.UpdatedAt);
            }
        }

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
            ServerSortOrder.Name => servers.OrderBy(server => server.DisplayName,
                StringComparer.CurrentCultureIgnoreCase),
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
                : visibleServers.Length == 0
                    ? Text("Empty")
                    : string.Empty;
        _actionPanel.Refresh();
    }

    private static bool MatchesSearch(ServerItem server, string search)
    {
        return server.DisplayName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
               server.Address.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               GetSourceName(server.SourceKind, server.SourceName)
                   .Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
               GetLocalTagNames(server.LocalTags).Any(name =>
                   name.Contains(search, StringComparison.CurrentCultureIgnoreCase)) ||
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
            ServerAction.Connect => IsRoomCategory
                ? _serverList.SelectedItem is RendezvousRoom
                : selected?.RuntimeStatus.Availability == ServerAvailability.Available,
            ServerAction.Add => _sourceCategoryDrawer.SelectedItem is SourceCategory.Local,
            ServerAction.Refresh or ServerAction.JoinCode => true,
            ServerAction.Favorite => !IsRoomCategory && selected is not null,
            ServerAction.Delete => _sourceCategoryDrawer.SelectedItem is SourceCategory.Local &&
                                   selected?.SourceKind == ServerSourceKind.Local,
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
            case ServerAction.JoinCode:
                ShowJoinCodeDialog();
                break;
            case ServerAction.Add:
                ShowAddDialog();
                break;
            case ServerAction.Refresh:
                RefreshSelectedSource();
                break;
            case ServerAction.Favorite when selected is not null:
                ToggleFavorite(selected);
                break;
            case ServerAction.Delete when selected is not null:
                ConfirmDelete(selected);
                break;
        }
    }

    private void ShowJoinCodeDialog()
    {
        DialogsManager.ShowDialog(this, new JoinCodeDialog(SettingsManager.Current.InvitationRelayPolicy,
            ConnectToRemoteSession));
    }

    private void ShowAddDialog()
    {
        DialogsManager.ShowDialog(null, new ContentRepositoryDialog(Text("AddTitle"), Text("NameLabel"),
            Text("AddressLabel"), Text("Add"), string.Empty, string.Empty, (name, address) =>
            {
                try
                {
                    var normalizedAddress = ServerAddress.Normalize(address);
                    if (ServerDirectoryManager.Current.Snapshot().LocalServers.Any(entry =>
                            entry.Address == normalizedAddress &&
                            entry.Tags.HasFlag(LocalServerTag.MyServer)))
                    {
                        DialogsManager.Alert(Text("DuplicateLocalServer"));
                        return false;
                    }

                    ServerDirectoryManager.Current.AddMyServer(name, address);
                    SelectLocalTagAndRefresh(LocalServerTag.MyServer);
                    return true;
                }
                catch (ArgumentException)
                {
                    DialogsManager.Alert(Text("InvalidServer"));
                    return false;
                }
            }));
    }

    private void ToggleFavorite(ServerItem server)
    {
        try
        {
            if (IsFavorite(server))
            {
                var address = ServerAddress.Normalize(server.Address);
                var entry = ServerDirectoryManager.Current.Snapshot().LocalServers
                    .First(item => item.Address == address);
                ServerDirectoryManager.Current.RemoveLocalTag(entry.Id, LocalServerTag.Favorite);
            }
            else
            {
                ServerDirectoryManager.Current.AddFavorite(server.DisplayName, server.Address);
            }

            if (_sourceCategoryDrawer.SelectedItem is SourceCategory.Local)
            {
                RefreshSelectedSource();
            }
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
                if (server.SourceKind == ServerSourceKind.Local)
                {
                    ServerDirectoryManager.Current.DeleteLocalServer(id);
                    RefreshSelectedSource();
                }
            }
        });
    }

    private void ConnectSelected()
    {
        if (IsRoomCategory && _serverList.SelectedItem is RendezvousRoom room)
        {
            ConnectToRemoteSession(room.CreateTarget(SettingsManager.Current.InvitationRelayPolicy));
            return;
        }

        if (_serverList.SelectedItem is not ServerItem server ||
            server.RuntimeStatus.Availability != ServerAvailability.Available)
        {
            return;
        }

        ConnectToAddress(server.DisplayName, server.Address);
    }

    public void ConnectToRemoteSession(NetworkTarget target)
    {
        if (_connectionProgress is not null)
        {
            return;
        }

        var busyDialog = new CancellableBusyDialog(Text("Connect"), false)
        {
            SmallMessage = Text("Loading"),
            ShowProgressMessage = false,
            CancelOnBack = true
        };
        _connectionProgress = busyDialog.Progress;
        var cancellationToken = busyDialog.Progress.CancellationToken;
        DialogsManager.ShowDialog(null, busyDialog);
        Task.Run(() => ServerConnectionPreparation.PrepareAsync(target, _connectionHttp, cancellationToken),
                cancellationToken)
            .ContinueWith(task => Dispatcher.Dispatch(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _ = task.Exception;
                    if (task.IsCompletedSuccessfully)
                    {
                        ReleasePrepared(task.Result, () => FinishConnectionPreparation(busyDialog));
                    }
                    else
                    {
                        FinishConnectionPreparation(busyDialog);
                    }

                    return;
                }

                if (!task.IsCompletedSuccessfully)
                {
                    FinishConnectionPreparation(busyDialog);
                    var exception = task.Exception?.GetBaseException();
                    Log.Warning($"Server connection preparation failed: {exception?.Message}");
                    DialogsManager.Alert(exception is RendezvousException failure
                        ? Text($"SignalError_{failure.Kind}")
                        : exception?.Message ?? Text("LoadFailed"));
                    return;
                }

                PrepareRemoteSessionAndConnect(task.Result, busyDialog);
            }), cancellationToken);
    }

    private void FinishConnectionPreparation(CancellableBusyDialog dialog)
    {
        DialogsManager.HideDialog(dialog);
        if (ReferenceEquals(_connectionProgress, dialog.Progress))
        {
            _connectionProgress = null;
        }

        dialog.Progress.CancellationTokenSource.Dispose();
    }

    private void ConnectToAddress(string name, string address)
    {
        try
        {
            var normalized = ServerAddress.Normalize(address);
            var uri = new Uri("tcp://" + normalized);
            var target = new AddressNetworkTarget(uri.DnsSafeHost,
                uri.Port > 0 ? uri.Port : SettingsManager.Current.ServerPort);
            ServerDirectoryManager.Current.RecordConnectionAttempt(name, normalized, DateTimeOffset.UtcNow);
            ConnectToRemoteSession(target);
        }
        catch (ArgumentException)
        {
            DialogsManager.Alert(LanguageManager.Get("Usual", "error"));
        }
    }

    private void SelectLocalTagAndRefresh(LocalServerTag tag)
    {
        _updatingSourceOptions = true;
        _sourceCategoryDrawer.SelectedItem = SourceCategory.Local;
        _updatingSourceOptions = false;
        ReloadSourceOptions();
        _updatingSourceOptions = true;
        _sourceDrawer.SelectedItem = _sourceDrawer.Items.Cast<SourceFilterOption>()
            .First(option => option.Tag == tag);
        _updatingSourceOptions = false;
        RefreshSelectedSource();
    }

    private void ReloadSourceOptions()
    {
        var selectedId = (_sourceDrawer.SelectedItem as SourceFilterOption)?.Source?.Id;
        var selectedTag = (_sourceDrawer.SelectedItem as SourceFilterOption)?.Tag ?? LocalServerTag.None;
        var selectedServiceId = (_sourceDrawer.SelectedItem as SourceFilterOption)?.Rendezvous?.Id;
        var options = _sourceCategoryDrawer.SelectedItem switch
        {
            SourceCategory.Local =>
            [
                new SourceFilterOption(null),
                new SourceFilterOption(null, LocalServerTag.MyServer),
                new SourceFilterOption(null, LocalServerTag.Favorite),
                new SourceFilterOption(null, LocalServerTag.Recent)
            ],
            SourceCategory.Lan => [new SourceFilterOption(null)],
            SourceCategory.Rendezvous => new[] { new SourceFilterOption(null) }
                .Concat(RendezvousManager.Current.Snapshot().Where(service => service.IsEnabled)
                    .Select(service => new SourceFilterOption(null, Rendezvous: service))).ToArray(),
            _ => new[] { new SourceFilterOption(null) }
                .Concat(GetCategorySources().Select(source => new SourceFilterOption(source))).ToArray()
        };
        _updatingSourceOptions = true;
        try
        {
            _sourceDrawer.SetItems(options);
            _sourceDrawer.SelectedItem = options.FirstOrDefault(option => option.Source?.Id == selectedId &&
                                                                          option.Tag == selectedTag &&
                                                                          option.Rendezvous?.Id == selectedServiceId) ??
                                         options[0];
            _sourceDrawer.IsEnabled = options.Length > 1;
            var sort = _sortDrawer.SelectedItem as ServerSortOrder? ?? ServerSortOrder.None;
            _sortDrawer.SetItems(IsRoomCategory
                ? new object[] { ServerSortOrder.None, ServerSortOrder.Name }
                : Enum.GetValues<ServerSortOrder>().Cast<object>().ToArray());
            _sortDrawer.SelectedItem = IsRoomCategory && sort is not (ServerSortOrder.None or ServerSortOrder.Name)
                ? ServerSortOrder.None
                : sort;
            _sortDrawer.IsEnabled = true;
            _actionPanel.Refresh();
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
            SourceCategory.Local => _sources.Where(source => source.Kind == ServerSourceKind.Local).ToArray(),
            SourceCategory.Lan => _sources.Where(source => source.Kind == ServerSourceKind.Lan).ToArray(),
            _ => _sources.Where(source => source.Kind == ServerSourceKind.Http).ToArray()
        };
    }

    private string GetActionText(object item)
    {
        if (item is not ServerAction action)
        {
            return string.Empty;
        }

        if (action == ServerAction.Favorite && _serverList.SelectedItem is ServerItem selected &&
            IsFavorite(selected))
        {
            return Text("Unfavorite");
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
        if (option.Rendezvous is { } service)
        {
            return service.Name;
        }

        var source = option.Source;
        if (option.Tag != LocalServerTag.None)
        {
            return GetLocalTagName(option.Tag);
        }

        if (source is null)
        {
            return _sourceCategoryDrawer.SelectedItem switch
            {
                SourceCategory.Local => Text("AllLocalTags"),
                SourceCategory.Lan => "—",
                SourceCategory.Rendezvous => Text("AllSignalingServices"),
                _ => Text("AllExternalSources")
            };
        }

        return GetSourceName(source.Kind, source.Name);
    }

    private static string GetSourceName(ServerSourceKind kind, string fallbackName)
    {
        return kind switch
        {
            ServerSourceKind.Local => Text("CategoryLocal"),
            ServerSourceKind.Lan => Text("Lan"),
            _ => fallbackName
        };
    }

    private static IEnumerable<string> GetLocalTagNames(LocalServerTag tags)
    {
        foreach (var tag in new[] { LocalServerTag.MyServer, LocalServerTag.Favorite, LocalServerTag.Recent })
        {
            if (tags.HasFlag(tag))
            {
                yield return GetLocalTagName(tag);
            }
        }
    }

    private static string GetLocalTagName(LocalServerTag tag)
    {
        return tag switch
        {
            LocalServerTag.MyServer => Text("MyServers"),
            LocalServerTag.Favorite => Text("Favorites"),
            LocalServerTag.Recent => Text("Recent"),
            _ => throw new ArgumentOutOfRangeException(nameof(tag))
        };
    }

    private static bool IsFavorite(ServerItem server)
    {
        if (server.LocalTags.HasFlag(LocalServerTag.Favorite))
        {
            return true;
        }

        var address = ServerAddress.Normalize(server.Address);
        return ServerDirectoryManager.Current.Snapshot().LocalServers.Any(entry =>
            entry.Address == address && entry.Tags.HasFlag(LocalServerTag.Favorite));
    }

    private void PrepareRemoteSessionAndConnect(ServerConnectionPreparation prepared, CancellableBusyDialog busyDialog)
    {
        var requiredProfile = prepared.Status.RequiredModProfile;
        if (requiredProfile is null)
        {
            FinishConnectionPreparation(busyDialog);
            ConnectPreparedRemoteSession(prepared);
            return;
        }

        busyDialog.SmallMessage = Text("CheckingMods");
        var cancellationToken = busyDialog.Progress.CancellationToken;
        Task.Run(() => ModRestartHelper.PrepareRemoteSession(
                SessionInfoManager.CreateRemoteClientSession(prepared.Target),
                requiredProfile, prepared.Status.TemporaryRepositories, cancellationToken,
                message => Dispatcher.Dispatch(() => busyDialog.SmallMessage = message)))
            .ContinueWith(task => Dispatcher.Dispatch(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _ = task.Exception;
                    ReleasePrepared(prepared, () => FinishConnectionPreparation(busyDialog));
                    return;
                }

                if (!task.IsCompletedSuccessfully)
                {
                    ReleasePrepared(prepared, () =>
                    {
                        FinishConnectionPreparation(busyDialog);
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            Log.Error($"Remote mod session preparation failed: {task.Exception}");
                            DialogsManager.Alert(Text("ModPreparationFailed"));
                        }
                        else
                        {
                            _ = task.Exception;
                        }
                    });
                    return;
                }

                if (!task.Result.RequiresRestart)
                {
                    FinishConnectionPreparation(busyDialog);
                    ConnectPreparedRemoteSession(prepared);
                    return;
                }

                ReleasePrepared(prepared, () =>
                {
                    FinishConnectionPreparation(busyDialog);
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        ConfirmRemoteModRestart(task.Result);
                    }
                });
            }));
    }

    private static void ReleasePrepared(ServerConnectionPreparation prepared, Action completed)
    {
        Task.Run(async () => await prepared.DisposeAsync()).ContinueWith(task => Dispatcher.Dispatch(() =>
        {
            if (task.IsFaulted)
            {
                Log.Warning($"Could not release server preparation: {task.Exception!.GetBaseException().Message}");
            }

            completed();
        }));
    }

    private static void ConnectPreparedRemoteSession(ServerConnectionPreparation prepared)
    {
        DialogsManager.HideAllDialogs();
        ScreensManager.SwitchScreen("GameLoading", string.Empty, string.Empty, prepared);
    }

    private static void ConfirmRemoteModRestart(RemoteModSessionPreparation result)
    {
        DialogsManager.ShowDialog(null, new MessageDialog(LanguageManager.GetContentWidgets(nameof(PlayScreen), 16),
            string.Format(LanguageManager.GetContentWidgets(nameof(PlayScreen), 17), result.RestartReason),
            LanguageManager.GetContentWidgets(nameof(PlayScreen), 18), LanguageManager.Get("Usual", "cancel"), button =>
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
