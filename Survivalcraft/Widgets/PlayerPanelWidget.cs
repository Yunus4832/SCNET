using System.Xml.Linq;

using Game.Commands;

namespace Game.Widgets;

/// <summary>
/// Full player operations panel. It is hosted by ComponentGui.ModalPanelWidget
/// and is therefore mutually exclusive with inventory, clothing and messages.
/// </summary>
public sealed class PlayerPanelWidget : CanvasWidget
{
    private const int _groupRequestPeriod = 15;

    private enum Tab
    {
        OnlinePlayers,
        Team,
        BlackList
    }

    private readonly PlayerInformationOverlayWidget _observationWidget;

    private readonly PlayerData _playerData;

    private readonly BevelledButtonWidget _onlineButton =
        MultiplayerUiStyle.CreateButton(MultiplayerUiStyle.Text("Players"), new Vector2(180f, 54f));

    private readonly BevelledButtonWidget _teamButton =
        MultiplayerUiStyle.CreateButton(MultiplayerUiStyle.Text("Team"), new Vector2(180f, 54f));

    private readonly BevelledButtonWidget _blackListButton =
        MultiplayerUiStyle.CreateButton(MultiplayerUiStyle.Text("Blacklist"), new Vector2(180f, 54f));

    private readonly BevelledButtonWidget _observationButton =
        MultiplayerUiStyle.CreateButton(string.Empty, new Vector2(140f, 54f));

    private readonly CanvasWidget _actionsHost;

    private readonly BevelledButtonWidget _blackListSelectedButton =
        MultiplayerUiStyle.CreateButton(MultiplayerUiStyle.Text("AddBlacklist"), new Vector2(140f, 54f));

    private readonly BevelledButtonWidget _inviteSelectedButton =
        MultiplayerUiStyle.CreateButton(MultiplayerUiStyle.Text("Invite"), new Vector2(140f, 54f));

    private readonly BevelledButtonWidget _playerFilterButton =
        MultiplayerUiStyle.CreateButton(string.Empty, new Vector2(140f, 54f));

    private readonly CanvasWidget _listHost;

    private readonly BevelledButtonWidget _removeBlackListButton =
        MultiplayerUiStyle.CreateButton(MultiplayerUiStyle.Text("RemoveBlacklist"), new Vector2(180f, 54f));

    private readonly BevelledButtonWidget _teamCreateButton =
        MultiplayerUiStyle.CreateButton(MultiplayerUiStyle.Text("CreateTeam"), new Vector2(180f, 54f));

    private readonly BevelledButtonWidget _teamJoinButton =
        MultiplayerUiStyle.CreateButton(MultiplayerUiStyle.Text("JoinTeam"), new Vector2(180f, 54f));

    private readonly BevelledButtonWidget _teamLeaveButton =
        MultiplayerUiStyle.CreateButton(MultiplayerUiStyle.Text("LeaveTeam"), new Vector2(180f, 54f));

    private readonly StackPanelWidget _onlineActions;

    private readonly StackPanelWidget _teamJoinActions;

    private readonly StackPanelWidget _teamMemberActions;

    private readonly StackPanelWidget _blackListActions;

    public readonly PlayerListWidget PlayerListWidget;

    public readonly TeamListWidget TeamListWidget;

    public readonly PlayerListWidget BlackPlayerListWidget;

    private Tab _tab;

    private double _lastGroupRequestTime = double.NegativeInfinity;

    private string _lastGroupKey = string.Empty;

    private PlayerListFilter _playerFilter;

    public PlayerPanelWidget(
        PlayerData playerData,
        PlayerInformationOverlayWidget observationWidget)
    {
        _playerData = playerData;
        _observationWidget = observationWidget;
        LoadContents(this, ContentManager.Get<XElement>("Widgets/PlayerPanelWidget"));
        var tabs = Children.Find<StackPanelWidget>("Tabs")!;
        _listHost = Children.Find<CanvasWidget>("ListHost")!;
        _actionsHost = Children.Find<CanvasWidget>("ActionsHost")!;
        var listSize = new Vector2(566f, 234f);
        PlayerListWidget = new PlayerListWidget(
            playerData,
            GetPlayerListKind(SettingsManager.Current.PlayerInformationFilter),
            false,
            listSize);
        TeamListWidget = new TeamListWidget(playerData, listSize);
        BlackPlayerListWidget = new PlayerListWidget(
            playerData,
            PlayerListWidget.ListKind.BlackList,
            false,
            listSize);
        _playerFilter = SettingsManager.Current.PlayerInformationFilter;
        _blackListSelectedButton.IsVisible = playerData.ServerManager;
        _onlineActions = CreateActionPanel(
            _inviteSelectedButton,
            _blackListSelectedButton,
            _playerFilterButton,
            _observationButton);
        _teamJoinActions = CreateActionPanel(_teamCreateButton, _teamJoinButton);
        _teamMemberActions = CreateActionPanel(_teamLeaveButton);
        _blackListActions = CreateActionPanel(_removeBlackListButton);

        tabs.Children.Add(_onlineButton);
        tabs.Children.Add(_teamButton);
        tabs.Children.Add(_blackListButton);
        _listHost.Children.Add(PlayerListWidget);
        _listHost.Children.Add(TeamListWidget);
        _listHost.Children.Add(BlackPlayerListWidget);
        _actionsHost.Children.Add(_onlineActions);
        _actionsHost.Children.Add(_teamJoinActions);
        _actionsHost.Children.Add(_teamMemberActions);
        _actionsHost.Children.Add(_blackListActions);

        PlayerListWidget.Players.SelectionChanged += UpdateActionButtons;
        TeamListWidget.Teams.SelectionChanged += UpdateActionButtons;
        BlackPlayerListWidget.Players.SelectionChanged += UpdateActionButtons;

        _lastGroupKey = playerData.GroupKey;
        ShowTab(Tab.OnlinePlayers);
        UpdatePlayerControls();
    }

    public void RefreshView()
    {
        ShowTab(_tab);
    }

    public override void Update()
    {
        if (_onlineButton.IsClicked)
        {
            ShowTab(Tab.OnlinePlayers);
        }
        else if (_teamButton.IsClicked)
        {
            ShowTab(Tab.Team);
        }
        else if (_blackListButton.IsClicked)
        {
            ShowTab(Tab.BlackList);
        }

        if (_observationButton.IsClicked)
        {
            _observationWidget.ToggleDisplay();
            UpdatePlayerControls();
        }

        if (_playerFilterButton.IsClicked)
        {
            SetPlayerFilter(_playerFilter == PlayerListFilter.All
                ? PlayerListFilter.SameTeam
                : PlayerListFilter.All);
        }

        if (_inviteSelectedButton.IsClicked)
        {
            InviteSelectedPlayer();
        }

        if (_blackListSelectedButton.IsClicked)
        {
            AddSelectedPlayerToBlackList();
        }

        if (_teamCreateButton.IsClicked)
        {
            ShowCreateTeamDialog();
        }

        if (_teamJoinButton.IsClicked)
        {
            JoinSelectedTeam();
        }

        if (_teamLeaveButton.IsClicked)
        {
            RequestLeaveTeam();
        }

        if (_removeBlackListButton.IsClicked)
        {
            RemoveSelectedPlayerFromBlackList();
        }

        if (_lastGroupKey != _playerData.GroupKey)
        {
            _lastGroupKey = _playerData.GroupKey;
            RefreshView();
        }
    }

    private void ShowTab(Tab tab)
    {
        _tab = tab;
        _onlineButton.IsChecked = tab == Tab.OnlinePlayers;
        _teamButton.IsChecked = tab == Tab.Team;
        _blackListButton.IsChecked = tab == Tab.BlackList;

        PlayerListWidget.IsVisible = tab == Tab.OnlinePlayers;
        TeamListWidget.IsVisible = tab == Tab.Team;
        BlackPlayerListWidget.IsVisible = tab == Tab.BlackList;
        _onlineActions.IsVisible = tab == Tab.OnlinePlayers;
        _teamJoinActions.IsVisible = tab == Tab.Team && string.IsNullOrEmpty(_playerData.GroupKey);
        _teamMemberActions.IsVisible = tab == Tab.Team && !string.IsNullOrEmpty(_playerData.GroupKey);
        _blackListActions.IsVisible = tab == Tab.BlackList;

        if (PlayerListWidget.IsVisible)
        {
            PlayerListWidget.RefreshList();
        }

        if (TeamListWidget.IsVisible)
        {
            TeamListWidget.RefreshList();
        }

        if (BlackPlayerListWidget.IsVisible)
        {
            BlackPlayerListWidget.RefreshList();
        }

        UpdateActionButtons();
    }

    private void UpdatePlayerControls()
    {
        _observationButton.Text = _observationWidget.DisplayEnabled
            ? MultiplayerUiStyle.Text("OverlayOn")
            : MultiplayerUiStyle.Text("OverlayOff");
        _observationButton.IsChecked = _observationWidget.DisplayEnabled;
        _playerFilterButton.Text = _playerFilter == PlayerListFilter.SameTeam
            ? MultiplayerUiStyle.Text("FilterTeam")
            : MultiplayerUiStyle.Text("FilterAll");
        _playerFilterButton.IsChecked = _playerFilter == PlayerListFilter.SameTeam;
    }

    private void SetPlayerFilter(PlayerListFilter filter)
    {
        _playerFilter = filter;
        SettingsManager.Current.PlayerInformationFilter = filter;
        PlayerListWidget.Kind = GetPlayerListKind(filter);
        _observationWidget.SetFilter(filter);
        UpdatePlayerControls();
        UpdateActionButtons();
    }

    private static PlayerListWidget.ListKind GetPlayerListKind(PlayerListFilter filter)
    {
        return filter == PlayerListFilter.SameTeam
            ? PlayerListWidget.ListKind.SameTeamPlayers
            : PlayerListWidget.ListKind.Players;
    }

    private void UpdateActionButtons()
    {
        var selectedOnlinePlayer = PlayerListWidget.Players.SelectedItem as PlayerData;
        var canManageOnlinePlayer =
            selectedOnlinePlayer is not null &&
            selectedOnlinePlayer.PlayerGUID != _playerData.PlayerGUID;
        _blackListSelectedButton.IsEnabled =
            _playerData.ServerManager &&
            canManageOnlinePlayer &&
            !_playerData.SubsystemPlayers.BlackPlayerGuidList.ContainsKey(
                selectedOnlinePlayer!.PlayerGUID.ToString());

        var canInvite = canManageOnlinePlayer &&
                        _playerData.GroupKey.Length > 0 &&
                        selectedOnlinePlayer!.GroupKey.Length == 0;
        _inviteSelectedButton.IsEnabled = canInvite;

        _teamLeaveButton.IsEnabled = _playerData.GroupKey.Length > 0;
        _teamCreateButton.IsEnabled = _playerData.GroupKey.Length == 0;
        _teamJoinButton.IsEnabled =
            _playerData.GroupKey.Length == 0 &&
            TeamListWidget.SelectedTeam is not null;
        _removeBlackListButton.IsEnabled =
            _playerData.ServerManager &&
            BlackPlayerListWidget.Players.SelectedItem is BlacklistPlayerData;
    }

    private void InviteSelectedPlayer()
    {
        if (PlayerListWidget.Players.SelectedItem is not PlayerData selectedPlayer)
        {
            return;
        }

        if (_playerData.GroupKey.Length > 0 && selectedPlayer.GroupKey.Length == 0)
        {
            SendGroupInvitation(selectedPlayer);
        }
    }

    private void ShowCreateTeamDialog()
    {
        if (_playerData.GroupKey.Length > 0)
        {
            ShowAlert("AlreadyInTeam");
            return;
        }

        DialogsManager.ShowDialog(
            _playerData.GameWidget.GuiWidget,
            new TextBoxDialog(
                MultiplayerUiStyle.Text("EnterTeamName"),
                string.Empty,
                64,
                name =>
                {
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        ShowAlert("TeamNameRequired");
                        return;
                    }

                    CommandGateway.Submit(
                        _playerData,
                        new CreateTeamCommand(name.Trim()));
                },
                invokeHandlerOnCancel: false));
    }

    private void JoinSelectedTeam()
    {
        if (_playerData.GroupKey.Length > 0)
        {
            ShowAlert("AlreadyInTeam");
            return;
        }

        if (TeamListWidget.SelectedTeam is not { } selectedTeam ||
            !Guid.TryParse(selectedTeam.GroupKey, out var groupKey) ||
            !_playerData.SubsystemPlayers.ServerGroups.ContainsKey(selectedTeam.GroupKey))
        {
            ShowAlert("SelectTeam");
            return;
        }

        DialogsManager.Confirm(
            FormatText("JoinTeamConfirm", selectedTeam.Name),
            button =>
            {
                if (button != MessageDialogButton.Button1 || !TryStartGroupRequest())
                {
                    return;
                }

                CommandGateway.Submit(
                    _playerData,
                    new RequestJoinTeamCommand(groupKey));
            },
            _playerData.GameWidget.GuiWidget);
    }

    private void SendGroupInvitation(PlayerData target)
    {
        if (!Guid.TryParse(_playerData.GroupKey, out _))
        {
            ShowAlert("CreateOrJoinTeamFirst");
            return;
        }

        DialogsManager.Confirm(
            FormatText("InvitePlayerConfirm", target.Name),
            button =>
            {
                if (button != MessageDialogButton.Button1 || !TryStartGroupRequest())
                {
                    return;
                }

                CommandGateway.Submit(
                    _playerData,
                    new InvitePlayerToTeamCommand(target.PlayerGUID));
            },
            _playerData.GameWidget.GuiWidget);
    }

    private void RequestLeaveTeam()
    {
        if (!Guid.TryParse(_playerData.GroupKey, out _) ||
            !_playerData.SubsystemPlayers.ServerGroups.TryGetValue(_playerData.GroupKey, out var group))
        {
            ShowAlert("NotInTeam");
            return;
        }

        DialogsManager.Confirm(
            FormatText("LeaveTeamConfirm", group.Name),
            button =>
            {
                if (button != MessageDialogButton.Button1 || !TryStartGroupRequest())
                {
                    return;
                }

                CommandGateway.Submit(_playerData, new LeaveTeamCommand());
            },
            _playerData.GameWidget.GuiWidget);
    }

    private bool TryStartGroupRequest()
    {
        var elapsed = Time.RealTime - _lastGroupRequestTime;
        if (elapsed < _groupRequestPeriod)
        {
            ShowAlert(
                "RetryAfterSeconds",
                MathUtils.Ceiling(_groupRequestPeriod - (float)elapsed));
            return false;
        }

        _lastGroupRequestTime = Time.RealTime;
        return true;
    }

    private void AddSelectedPlayerToBlackList()
    {
        if (PlayerListWidget.Players.SelectedItem is not PlayerData selectedPlayer ||
            selectedPlayer.PlayerGUID == _playerData.PlayerGUID)
        {
            return;
        }

        DialogsManager.Confirm(
            FormatText("AddBlacklistConfirm", selectedPlayer.Name),
            button =>
            {
                if (button != MessageDialogButton.Button1)
                {
                    return;
                }

                _playerData.SubsystemPlayers.AddBlackList(selectedPlayer);
                RefreshView();
            },
            _playerData.GameWidget.GuiWidget);
    }

    private void RemoveSelectedPlayerFromBlackList()
    {
        if (BlackPlayerListWidget.Players.SelectedItem is not BlacklistPlayerData selectedPlayer)
        {
            return;
        }

        DialogsManager.Confirm(
            FormatText("RemoveBlacklistConfirm", selectedPlayer.Name),
            button =>
            {
                if (button != MessageDialogButton.Button1)
                {
                    return;
                }

                _playerData.SubsystemPlayers.BlackPlayerGuidList.Remove(
                    selectedPlayer.PlayerGUID.ToString());
                BlackPlayerListWidget.RefreshList();
                UpdateActionButtons();
            },
            _playerData.GameWidget.GuiWidget);
    }

    private void ShowAlert(string key, params object[] args)
    {
        DialogsManager.Alert(
            LanguageManager.Warning,
            FormatText(key, args),
            _playerData.GameWidget.GuiWidget);
    }

    private static string FormatText(string key, params object[] args)
    {
        var text = MultiplayerUiStyle.Text(key);
        return args.Length > 0 ? string.Format(text, args) : text;
    }

    private static StackPanelWidget CreateActionPanel(params Widget[] buttons)
    {
        var panel = new StackPanelWidget
        {
            Direction = LayoutDirection.Horizontal,
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center
        };
        foreach (var button in buttons)
        {
            panel.Children.Add(button);
        }

        return panel;
    }
}
