using System.Xml.Linq;

using Game.Characters;
using Game.Commands;
using Game.Network;

namespace Game.Widgets;

/// <summary>
///     Full player operations panel. It is hosted by ComponentGui.ModalPanelWidget
///     and is therefore mutually exclusive with inventory, clothing and messages.
/// </summary>
public sealed class MultiplayerPanelWidget : CanvasWidget
{
    private enum Tab
    {
        OnlinePlayers = 0,

        Team = 1
    }

    private readonly CharacterInformationOverlayWidget _observationWidget;

    private readonly CharacterData _characterData;

    private Guid CurrentTeamId =>
        CharacterTeamQuery.GetTeamId(_characterData.Project, _characterData.CharacterId);

    private readonly BevelledButtonWidget _onlineButton =
        MultiplayerUiStyle.CreateButton(MultiplayerUiStyle.Text("Characters"), new Vector2(180f, 54f));

    private readonly BevelledButtonWidget _teamButton =
        MultiplayerUiStyle.CreateButton(MultiplayerUiStyle.Text("Team"), new Vector2(180f, 54f));

    private readonly BevelledButtonWidget _observationButton =
        MultiplayerUiStyle.CreateButton(string.Empty, new Vector2(140f, 54f));

    private readonly CanvasWidget _actionsHost;

    private readonly BevelledButtonWidget _inviteSelectedButton =
        MultiplayerUiStyle.CreateButton(MultiplayerUiStyle.Text("Invite"), new Vector2(140f, 54f));

    private readonly BevelledButtonWidget _playerFilterButton =
        MultiplayerUiStyle.CreateButton(string.Empty, new Vector2(140f, 54f));

    private readonly CanvasWidget _listHost;

    private readonly BevelledButtonWidget _teamCreateButton =
        MultiplayerUiStyle.CreateButton(MultiplayerUiStyle.Text("CreateTeam"), new Vector2(180f, 54f));

    private readonly BevelledButtonWidget _teamJoinButton =
        MultiplayerUiStyle.CreateButton(MultiplayerUiStyle.Text("JoinTeam"), new Vector2(180f, 54f));

    private readonly BevelledButtonWidget _teamLeaveButton =
        MultiplayerUiStyle.CreateButton(MultiplayerUiStyle.Text("LeaveTeam"), new Vector2(180f, 54f));

    private readonly StackPanelWidget _onlineActions;

    private readonly StackPanelWidget _teamJoinActions;

    private readonly StackPanelWidget _teamMemberActions;

    public readonly CharacterListWidget CharacterListWidget;

    public readonly TeamListWidget TeamListWidget;

    private Tab _tab;

    private Guid _lastTeamId = Guid.Empty;

    private CharacterListFilter _playerFilter;

    public MultiplayerPanelWidget(
        CharacterData characterData,
        CharacterInformationOverlayWidget observationWidget
    )
    {
        _characterData = characterData;
        _observationWidget = observationWidget;
        LoadContents(this, ContentManager.Get<XElement>("Widgets/MultiplayerPanelWidget"));
        var tabs = Children.Find<StackPanelWidget>("Tabs")!;
        _listHost = Children.Find<CanvasWidget>("ListHost")!;
        _actionsHost = Children.Find<CanvasWidget>("ActionsHost")!;
        var listSize = new Vector2(566f, 234f);
        CharacterListWidget = new CharacterListWidget(
            characterData,
            GetPlayerListKind(SettingsManager.Current.CharacterInformationFilter),
            false,
            listSize
        );
        TeamListWidget = new TeamListWidget(characterData, listSize);
        _playerFilter = SettingsManager.Current.CharacterInformationFilter;
        _onlineActions = CreateActionPanel(
            _inviteSelectedButton,
            _playerFilterButton,
            _observationButton
        );
        _teamJoinActions = CreateActionPanel(_teamCreateButton, _teamJoinButton);
        _teamMemberActions = CreateActionPanel(_teamLeaveButton);

        tabs.Children.Add(_onlineButton);
        tabs.Children.Add(_teamButton);
        _listHost.Children.Add(CharacterListWidget);
        _listHost.Children.Add(TeamListWidget);
        _actionsHost.Children.Add(_onlineActions);
        _actionsHost.Children.Add(_teamJoinActions);
        _actionsHost.Children.Add(_teamMemberActions);

        CharacterListWidget.Characters.SelectionChanged += UpdateActionButtons;
        TeamListWidget.Teams.SelectionChanged += UpdateActionButtons;

        _lastTeamId = CurrentTeamId;
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

        if (_observationButton.IsClicked)
        {
            _observationWidget.ToggleDisplay();
            UpdatePlayerControls();
        }

        if (_playerFilterButton.IsClicked)
        {
            SetPlayerFilter(
                _playerFilter == CharacterListFilter.All
                    ? CharacterListFilter.SameTeam
                    : CharacterListFilter.All
            );
        }

        if (_inviteSelectedButton.IsClicked)
        {
            InviteSelectedPlayer();
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

        if (_lastTeamId != CurrentTeamId)
        {
            _lastTeamId = CurrentTeamId;
            RefreshView();
        }
    }

    private void ShowTab(Tab tab)
    {
        _tab = tab;
        _onlineButton.IsChecked = tab == Tab.OnlinePlayers;
        _teamButton.IsChecked = tab == Tab.Team;

        CharacterListWidget.IsVisible = tab == Tab.OnlinePlayers;
        TeamListWidget.IsVisible = tab == Tab.Team;
        _onlineActions.IsVisible = tab == Tab.OnlinePlayers;
        _teamJoinActions.IsVisible = tab == Tab.Team && (CurrentTeamId == Guid.Empty);
        _teamMemberActions.IsVisible = tab == Tab.Team && !(CurrentTeamId == Guid.Empty);

        if (CharacterListWidget.IsVisible)
        {
            CharacterListWidget.RefreshList();
        }

        if (TeamListWidget.IsVisible)
        {
            TeamListWidget.RefreshList();
        }

        UpdateActionButtons();
    }

    private void UpdatePlayerControls()
    {
        MultiplayerUiStyle.SetButtonText(
            _observationButton,
            _observationWidget.DisplayEnabled
                ? MultiplayerUiStyle.Text("OverlayOn")
                : MultiplayerUiStyle.Text("OverlayOff")
        );
        _observationButton.IsChecked = _observationWidget.DisplayEnabled;
        MultiplayerUiStyle.SetButtonText(
            _playerFilterButton,
            _playerFilter == CharacterListFilter.SameTeam
                ? MultiplayerUiStyle.Text("FilterTeam")
                : MultiplayerUiStyle.Text("FilterAll")
        );
        _playerFilterButton.IsChecked = _playerFilter == CharacterListFilter.SameTeam;
    }

    private void SetPlayerFilter(CharacterListFilter filter)
    {
        _playerFilter = filter;
        SettingsManager.Current.CharacterInformationFilter = filter;
        CharacterListWidget.Kind = GetPlayerListKind(filter);
        _observationWidget.SetFilter(filter);
        UpdatePlayerControls();
        UpdateActionButtons();
    }

    private static CharacterListWidget.ListKind GetPlayerListKind(CharacterListFilter filter)
    {
        return filter == CharacterListFilter.SameTeam
            ? CharacterListWidget.ListKind.SameTeamCharacters
            : CharacterListWidget.ListKind.Characters;
    }

    private void UpdateActionButtons()
    {
        var selectedOnlinePlayer = CharacterListWidget.Characters.SelectedItem as CharacterData;
        var canManageOnlinePlayer =
            selectedOnlinePlayer is not null &&
            selectedOnlinePlayer.CharacterId != _characterData.CharacterId;
        var canInvite = canManageOnlinePlayer &&
                        CurrentTeamId != Guid.Empty &&
                        CharacterTeamQuery.GetTeamId(_characterData.Project, selectedOnlinePlayer!.CharacterId) ==
                        Guid.Empty;
        _inviteSelectedButton.IsEnabled = canInvite;

        _teamLeaveButton.IsEnabled = CurrentTeamId != Guid.Empty;
        _teamCreateButton.IsEnabled = CurrentTeamId == Guid.Empty;
        _teamJoinButton.IsEnabled =
            CurrentTeamId == Guid.Empty &&
            TeamListWidget.SelectedTeam is not null;
    }

    private void InviteSelectedPlayer()
    {
        if (CharacterListWidget.Characters.SelectedItem is not CharacterData selectedPlayer)
        {
            return;
        }

        if (CurrentTeamId != Guid.Empty &&
            CharacterTeamQuery.GetTeamId(_characterData.Project, selectedPlayer.CharacterId) == Guid.Empty)
        {
            SendTeamInvitation(selectedPlayer);
        }
    }

    private void ShowCreateTeamDialog()
    {
        if (CurrentTeamId != Guid.Empty)
        {
            ShowAlert("AlreadyInTeam");
            return;
        }

        DialogsManager.ShowDialog(
            _characterData.GameWidget.GuiWidget,
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
                        _characterData,
                        new CreateTeamCommand(name.Trim())
                    );
                },
                invokeHandlerOnCancel: false
            )
        );
    }

    private void JoinSelectedTeam()
    {
        if (CurrentTeamId != Guid.Empty)
        {
            ShowAlert("AlreadyInTeam");
            return;
        }

        if (TeamListWidget.SelectedTeam is not { } selectedTeam ||
            !_characterData.Project.FindSubsystem<SubsystemTeams>(true)!.Store.Teams.ContainsKey(selectedTeam.TeamId))
        {
            ShowAlert("SelectTeam");
            return;
        }

        DialogsManager.Confirm(
            FormatText("JoinTeamConfirm", selectedTeam.Name),
            button =>
            {
                if (button != MessageDialogButton.Button1)
                {
                    return;
                }

                CommandGateway.Submit(
                    _characterData,
                    new RequestJoinTeamCommand(selectedTeam.TeamId)
                );
            },
            _characterData.GameWidget.GuiWidget
        );
    }

    private void SendTeamInvitation(CharacterData target)
    {
        if (CurrentTeamId == Guid.Empty)
        {
            ShowAlert("CreateOrJoinTeamFirst");
            return;
        }

        var address = PlayerSessionQuery.GetController(target.CharacterId);
        if (address is null) { return; }

        DialogsManager.Confirm(
            FormatText("InvitePlayerConfirm", target.Name),
            button =>
            {
                if (button != MessageDialogButton.Button1)
                {
                    return;
                }

                CommandGateway.Submit(
                    _characterData,
                    new InvitePlayerToTeamCommand(address.Value)
                );
            },
            _characterData.GameWidget.GuiWidget
        );
    }

    private void RequestLeaveTeam()
    {
        if (_characterData.Project.FindSubsystem<SubsystemTeams>(true)!.Store.Find(CurrentTeamId) is not { } group)
        {
            ShowAlert("NotInTeam");
            return;
        }

        DialogsManager.Confirm(
            FormatText("LeaveTeamConfirm", group.Name),
            button =>
            {
                if (button != MessageDialogButton.Button1)
                {
                    return;
                }

                CommandGateway.Submit(_characterData, new LeaveTeamCommand());
            },
            _characterData.GameWidget.GuiWidget
        );
    }

    private void ShowAlert(string key, params object[] args)
    {
        DialogsManager.Alert(
            LanguageManager.Warning,
            FormatText(key, args),
            _characterData.GameWidget.GuiWidget
        );
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
