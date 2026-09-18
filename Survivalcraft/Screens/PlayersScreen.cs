using System.Xml.Linq;

namespace Game.Screens;

public class PlayersScreen : Screen
{
    private readonly CharacterSkinsCache _characterSkinsCache = new();

    private readonly StackPanelWidget _playersPanel;

    private SubsystemPlayers _subsystemPlayers = null!;

    public PlayersScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/PlayersScreen");
        LoadContents(this, node);
        _playersPanel = Children.Find<StackPanelWidget>("PlayersPanel")!;
    }

    public override void Enter(object[] parameters)
    {
        _subsystemPlayers = (SubsystemPlayers)parameters[0];
        UpdatePlayersPanel();
    }

    public override void Leave()
    {
        _subsystemPlayers = null!;
        _characterSkinsCache.Clear();
        _playersPanel.Children.Clear();
    }

    public override void Update()
    {
        GameManager.UpdateProject();
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Game");
        }
    }

    private void UpdatePlayersPanel()
    {
        _playersPanel.Children.Clear();
        foreach (var playerData in _subsystemPlayers.PlayersData)
        {
            _playersPanel.Children.Add(new PlayerWidget(playerData, _characterSkinsCache));
        }
    }

    public void PlayersChanged(PlayerData playerData)
    {
        UpdatePlayersPanel();
    }
}
