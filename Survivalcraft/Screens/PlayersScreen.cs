using System.Xml.Linq;

using Engine.Media;

using Game.Network;
using Game.Network.Enums;

namespace Game.Screens;

public class PlayersScreen : Screen
{
    private readonly ButtonWidget _addPlayerButton;

    private readonly CharacterSkinsCache _characterSkinsCache = new();

    private readonly StackPanelWidget _playersPanel;

    private readonly ButtonWidget _screenLayoutButton;

    private SubsystemPlayers _subsystemPlayers = null!;

    public PlayersScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/PlayersScreen");
        LoadContents(this, node);
        _playersPanel = Children.Find<StackPanelWidget>("PlayersPanel")!;
        _addPlayerButton = Children.Find<ButtonWidget>("AddPlayerButton")!;
        _screenLayoutButton = Children.Find<ButtonWidget>("ScreenLayoutButton")!;
    }

    public override void Enter(object[] parameters)
    {
        _subsystemPlayers = (SubsystemPlayers)parameters[0];
        UpdatePlayersPanel();
        _addPlayerButton.IsVisible = CommonLib.WorkType == WorkType.Local;
        _screenLayoutButton.IsVisible = CommonLib.WorkType == WorkType.Local;
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
        if (_addPlayerButton.IsClicked)
        {
            if (CommonLib.WorkType != WorkType.Local)
            {
                DialogsManager.Alert("不可进行操作");
            }
            else
            {
                var subsystemGameInfo = _subsystemPlayers.Project.FindSubsystem<SubsystemGameInfo>(true)!;
                if (subsystemGameInfo.WorldSettings.GameMode == GameMode.Cruel)
                {
                    DialogsManager.ShowDialog(
                        null,
                        new MessageDialog(LanguageManager.Unavailable, "不可在参考模式添加玩家", LanguageManager.Ok)
                    );
                }
                else if (subsystemGameInfo.WorldSettings.GameMode == GameMode.Adventure)
                {
                    DialogsManager.ShowDialog(
                        null,
                        new MessageDialog(LanguageManager.Unavailable, "不可在冒险模式添加玩家", LanguageManager.Ok)
                    );
                }
                else if (_subsystemPlayers.PlayersData.Count >=
                         GameManager.Project!.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.MaxOnlinePlayerCount)
                {
                    DialogsManager.ShowDialog(
                        null,
                        new MessageDialog(
                            LanguageManager.Unavailable,
                            $"超出最大玩家数量{GameManager.Project!.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.MaxOnlinePlayerCount}",
                            LanguageManager.Ok
                        )
                    );
                }
                else
                {
                    ScreensManager.SwitchScreen("Player", PlayerScreen.Mode.Add, _subsystemPlayers.Project);
                }
            }
        }

        if (_screenLayoutButton.IsClicked)
        {
            ScreenLayout[] array = [];
            if (_subsystemPlayers.PlayersData.Count == 1)
            {
                array = new ScreenLayout[1];
            }
            else if (_subsystemPlayers.PlayersData.Count == 2)
            {
                array =
                [
                    ScreenLayout.DoubleVertical,
                    ScreenLayout.DoubleHorizontal,
                    ScreenLayout.DoubleOpposite
                ];
            }
            else if (_subsystemPlayers.PlayersData.Count == 3)
            {
                array =
                [
                    ScreenLayout.TripleVertical,
                    ScreenLayout.TripleHorizontal,
                    ScreenLayout.TripleEven,
                    ScreenLayout.TripleOpposite
                ];
            }
            else if (_subsystemPlayers.PlayersData.Count == 4)
            {
                array =
                [
                    ScreenLayout.Quadruple,
                    ScreenLayout.QuadrupleOpposite
                ];
            }

            if (array.Length != 0)
            {
                DialogsManager.ShowDialog(
                    null,
                    new ListSelectionDialog(
                        "Select Screen Layout",
                        array,
                        80f,
                        delegate(object o)
                        {
                            var str = o.ToString();
                            var name = "Textures/Atlas/ScreenLayout" + str;
                            return new StackPanelWidget
                            {
                                Direction = LayoutDirection.Horizontal,
                                VerticalAlignment = WidgetAlignment.Center,
                                Children =
                                {
                                    new RectangleWidget
                                    {
                                        Size = new Vector2(98f, 56f),
                                        Subtexture = ContentManager.Get<Subtexture>(name),
                                        FillColor = Color.White,
                                        OutlineColor = Color.Transparent,
                                        Margin = new Vector2(10f, 0f)
                                    },
                                    new StackPanelWidget
                                    {
                                        Direction = LayoutDirection.Vertical,
                                        VerticalAlignment = WidgetAlignment.Center,
                                        Margin = new Vector2(10f, 0f),
                                        Children =
                                        {
                                            new LabelWidget
                                            {
                                                Text = StringsManager.GetString("ScreenLayout." + str + ".Name"),
                                                Font = ContentManager.Get<BitmapFont>("Fonts/Pericles")
                                            },
                                            new LabelWidget
                                            {
                                                Text = StringsManager.GetString("ScreenLayout." + str + ".Description"),
                                                Font = ContentManager.Get<BitmapFont>("Fonts/Pericles"),
                                                Color = Color.Gray
                                            }
                                        }
                                    }
                                }
                            };
                        },
                        delegate(object o)
                        {
                            if (_subsystemPlayers.PlayersData.Count == 1)
                            {
                                SettingsManager.Current.ScreenLayout1 = (ScreenLayout)o;
                            }

                            if (_subsystemPlayers.PlayersData.Count == 2)
                            {
                                SettingsManager.Current.ScreenLayout2 = (ScreenLayout)o;
                            }

                            if (_subsystemPlayers.PlayersData.Count == 3)
                            {
                                SettingsManager.Current.ScreenLayout3 = (ScreenLayout)o;
                            }

                            if (_subsystemPlayers.PlayersData.Count == 4)
                            {
                                SettingsManager.Current.ScreenLayout4 = (ScreenLayout)o;
                            }
                        }
                    )
                );
            }
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Game");
        }
    }

    private void UpdatePlayersPanel()
    {
        _playersPanel.Children.Clear();
        foreach (var playersDatum in _subsystemPlayers.PlayersData)
        {
            if (_characterSkinsCache != null)
            {
                _playersPanel.Children.Add(new PlayerWidget(playersDatum, _characterSkinsCache));
            }
        }
    }

    public void PlayersChanged(PlayerData playerData)
    {
        UpdatePlayersPanel();
    }
}
