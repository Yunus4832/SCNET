using System.Xml.Linq;

using Engine.Graphics;

using Game.Network;

namespace Game.Dialogs;

public class GameMenuDialog : Dialog
{
    private const string _typeName = nameof(GameMenuDialog);

    private static bool _increaseDetailDialogShown;

    private static bool _decreaseDetailDialogShown;


    private readonly bool _adventureRestartExists;

    private readonly ComponentCharacter _componentCharacter;

    public GameMenuDialog(ComponentCharacter componentCharacter)
    {
        var node = ContentManager.Get<XElement>("Dialogs/GameMenuDialog");
        LoadContents(this, node);
        var statsPanel = Children.Find<StackPanelWidget>("StatsPanel")!;
        _componentCharacter = componentCharacter;
        if (CommonLib.WorkType != WorkType.Client && GameManager.WorldInfo != null)
        {
            _adventureRestartExists =
                WorldsManager.SnapshotExists(GameManager.WorldInfo.DirectoryName, "AdventureRestart");
        }

        if (!_increaseDetailDialogShown &&
            PerformanceManager.LongTermAverageFrameTime.HasValue &&
            PerformanceManager.LongTermAverageFrameTime.Value * 1000f < 25f &&
            (SettingsManager.Current.VisibilityRange <= 64 ||
             SettingsManager.Current.ResolutionMode == ResolutionMode.Low))
        {
            _increaseDetailDialogShown = true;
            DialogsManager.ShowDialog(
                ParentWidget,
                new MessageDialog(
                    LanguageManager.Get(_typeName, 1),
                    LanguageManager.Get(_typeName, 2),
                    LanguageManager.Get("Usual", "ok")
                )
            );
        }

        if (!_decreaseDetailDialogShown &&
            PerformanceManager.LongTermAverageFrameTime.HasValue &&
            PerformanceManager.LongTermAverageFrameTime.Value * 1000f > 50f &&
            (SettingsManager.Current.VisibilityRange >= 64 ||
             SettingsManager.Current.ResolutionMode == ResolutionMode.High))
        {
            _decreaseDetailDialogShown = true;
            DialogsManager.ShowDialog(
                ParentWidget,
                new MessageDialog(
                    LanguageManager.Get(_typeName, 3),
                    LanguageManager.Get(_typeName, 4),
                    LanguageManager.Get("Usual", "ok")
                )
            );
        }

        statsPanel.Children.Clear();
        var project = componentCharacter.Project;
        var characterData = componentCharacter.CharacterData;
        var characterStats = componentCharacter.CharacterStats;
        var subsystemGameInfo = project.FindSubsystem<SubsystemGameInfo>(true)!;
        var subsystemFurnitureBlockBehavior = project.FindSubsystem<SubsystemFurnitureBlockBehavior>(true)!;
        var font = ContentManager.Get<BitmapFont>("Fonts/Pericles");
        var font2 = ContentManager.Get<BitmapFont>("Fonts/Pericles");
        var white = Color.White;
        var stackPanelWidget = new StackPanelWidget
        {
            Direction = LayoutDirection.Vertical,
            HorizontalAlignment = WidgetAlignment.Center
        };
        statsPanel.Children.Add(stackPanelWidget);
        stackPanelWidget.Children.Add(
            new LabelWidget
            {
                Text = LanguageManager.Get(_typeName, 5),
                Font = font,
                HorizontalAlignment = WidgetAlignment.Center,
                Margin = new Vector2(0f, 10f),
                Color = white
            }
        );
        if (CommonLib.WorkType == WorkType.Client && CommonLib.Net.Server?.Peer is { IsConnected: true } connection)
        {
            AddStat(
                stackPanelWidget,
                LanguageManager.GetContentWidgets(_typeName, "ConnectionPath"),
                LanguageManager.GetContentWidgets(_typeName, $"Path{connection.PathKind}")
            );
        }

        AddStat(
            stackPanelWidget,
            LanguageManager.Get(_typeName, 6),
            LanguageManager.Get("GameMode", subsystemGameInfo.WorldSettings.GameMode.ToString()) +
            ", " +
            LanguageManager.Get(
                "EnvironmentBehaviorMode",
                subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode.ToString()
            )
        );
        AddStat(
            stackPanelWidget,
            LanguageManager.Get(_typeName, 7),
            StringsManager.GetString(
                "TerrainGenerationMode",
                TerrainGenerationModes.ToDisplayMode(subsystemGameInfo.WorldSettings.TerrainGenerationMode),
                "Name"
            )
        );
        var seed = subsystemGameInfo.WorldSettings.Seed;
        AddStat(
            stackPanelWidget,
            LanguageManager.Get(_typeName, 8),
            !string.IsNullOrEmpty(seed) ? seed : LanguageManager.Get(_typeName, 9)
        );
        AddStat(
            stackPanelWidget,
            LanguageManager.Get(_typeName, 10),
            WorldOptionsScreen.FormatOffset(subsystemGameInfo.WorldSettings.SeaLevelOffset)
        );
        AddStat(
            stackPanelWidget,
            LanguageManager.Get(_typeName, 11),
            WorldOptionsScreen.FormatOffset(subsystemGameInfo.WorldSettings.TemperatureOffset)
        );
        AddStat(
            stackPanelWidget,
            LanguageManager.Get(_typeName, 12),
            WorldOptionsScreen.FormatOffset(subsystemGameInfo.WorldSettings.HumidityOffset)
        );
        AddStat(stackPanelWidget, LanguageManager.Get(_typeName, 13), subsystemGameInfo.WorldSettings.BiomeSize + "x");
        if (subsystemGameInfo.WorldSettings.AreSeasonsChanging)
        {
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 96),
                subsystemGameInfo.WorldSettings.YearDays + " days"
            );
        }

        var value0 = subsystemGameInfo.WorldSettings.AreSeasonsChanging ? "" : "(fixed season)";
        AddStat(
            stackPanelWidget,
            LanguageManager.Get(_typeName, 97),
            SubsystemSeasons.GetTimeOfYearName(subsystemGameInfo.WorldSettings.TimeOfYear),
            value0,
            SubsystemSeasons.GetTimeOfYearColor(subsystemGameInfo.WorldSettings.TimeOfYear)
        );

        var num = 0;
        for (var i = 0; i < FurnitureDesign.MaxDesign; i++)
        {
            if (subsystemFurnitureBlockBehavior.GetDesign(i) != null)
            {
                num++;
            }
        }

        AddStat(stackPanelWidget, LanguageManager.Get(_typeName, 14), $"{num}/{FurnitureDesign.MaxDesign}");
        stackPanelWidget.Children.Add(
            new LabelWidget
            {
                Text = LanguageManager.Get(_typeName, 17),
                Font = font,
                HorizontalAlignment = WidgetAlignment.Center,
                Margin = new Vector2(0f, 10f),
                Color = white
            }
        );
        AddStat(stackPanelWidget, LanguageManager.Get(_typeName, 18), characterData.Name);
        AddStat(stackPanelWidget, LanguageManager.Get(_typeName, 19), characterData.CharacterSex.ToString());
        var value = characterData.FirstSpawnTime >= 0.0
            ? ((subsystemGameInfo.TotalElapsedGameTime - characterData.FirstSpawnTime) / 1200.0).ToString("N1") +
              LanguageManager.Get(_typeName, 20)
            : LanguageManager.Get(_typeName, 21);
        AddStat(stackPanelWidget, LanguageManager.Get(_typeName, 22), value);
        var value2 = characterData.LastSpawnTime >= 0.0
            ? ((subsystemGameInfo.TotalElapsedGameTime - characterData.LastSpawnTime) / 1200.0).ToString("N1") +
              LanguageManager.Get(_typeName, 23)
            : LanguageManager.Get(_typeName, 24);
        AddStat(stackPanelWidget, LanguageManager.Get(_typeName, 25), value2);
        AddStat(
            stackPanelWidget,
            LanguageManager.Get(_typeName, 26),
            MathUtils.Max(characterData.SpawnsCount - 1, 0).ToString("N0") + LanguageManager.Get(_typeName, 27)
        );
        AddStat(
            stackPanelWidget,
            LanguageManager.Get(_typeName, 28),
            string.Format(
                LanguageManager.Get(_typeName, 29),
                ((int)MathUtils.Floor(characterStats.HighestLevel)).ToString("N0")
            )
        );

        var position = componentCharacter.ComponentBody.Position;
        if (subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative)
        {
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 30),
                string.Format(
                    LanguageManager.Get(_typeName, 31),
                    $"{position.X:0}",
                    $"{position.Z:0}",
                    $"{position.Y:0}"
                )
            );
        }
        else
        {
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 30),
                string.Format(
                    LanguageManager.Get(_typeName, 32),
                    LanguageManager.Get("GameMode", subsystemGameInfo.WorldSettings.GameMode.ToString())
                )
            );
        }

        if (!TerrainGenerationModes.IsPre21(subsystemGameInfo.WorldSettings.TerrainGenerationMode))
        {
            stackPanelWidget.Children.Add(
                new LabelWidget
                {
                    Text = LanguageManager.Get(_typeName, 33),
                    Font = font,
                    HorizontalAlignment = WidgetAlignment.Center,
                    Margin = new Vector2(0f, 10f),
                    Color = white
                }
            );
            AddStat(stackPanelWidget, LanguageManager.Get(_typeName, 34), characterStats.PlayerKills.ToString("N0"));
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 35),
                characterStats.LandCreatureKills.ToString("N0")
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 36),
                characterStats.WaterCreatureKills.ToString("N0")
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 37),
                characterStats.AirCreatureKills.ToString("N0")
            );
            AddStat(stackPanelWidget, LanguageManager.Get(_typeName, 38), characterStats.MeleeAttacks.ToString("N0"));
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 39),
                characterStats.MeleeHits.ToString("N0"),
                $"({(characterStats.MeleeHits == 0L ? 0.0 : characterStats.MeleeHits / (double)characterStats.MeleeAttacks * 100.0):0}%)"
            );
            AddStat(stackPanelWidget, LanguageManager.Get(_typeName, 40), characterStats.RangedAttacks.ToString("N0"));
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 41),
                characterStats.RangedHits.ToString("N0"),
                $"({(characterStats.RangedHits == 0L ? 0.0 : characterStats.RangedHits / (double)characterStats.RangedAttacks * 100.0):0}%)"
            );
            AddStat(stackPanelWidget, LanguageManager.Get(_typeName, 42), characterStats.HitsReceived.ToString("N0"));
            stackPanelWidget.Children.Add(
                new LabelWidget
                {
                    Text = LanguageManager.Get(_typeName, 43),
                    Font = font,
                    HorizontalAlignment = WidgetAlignment.Center,
                    Margin = new Vector2(0f, 10f),
                    Color = white
                }
            );
            AddStat(stackPanelWidget, LanguageManager.Get(_typeName, 44), characterStats.BlocksDug.ToString("N0"));
            AddStat(stackPanelWidget, LanguageManager.Get(_typeName, 45), characterStats.BlocksPlaced.ToString("N0"));
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 46),
                characterStats.BlocksInteracted.ToString("N0")
            );
            AddStat(stackPanelWidget, LanguageManager.Get(_typeName, 47), characterStats.ItemsCrafted.ToString("N0"));
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 48),
                characterStats.FurnitureItemsMade.ToString("N0")
            );
            stackPanelWidget.Children.Add(
                new LabelWidget
                {
                    Text = LanguageManager.Get(_typeName, 49),
                    Font = font,
                    HorizontalAlignment = WidgetAlignment.Center,
                    Margin = new Vector2(0f, 10f),
                    Color = white
                }
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 50),
                FormatDistance(characterStats.DistanceTravelled)
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 51),
                FormatDistance(characterStats.DistanceWalked),
                $"({(characterStats.DistanceTravelled > 0.0 ? characterStats.DistanceWalked / characterStats.DistanceTravelled * 100.0 : 0.0):0.0}%)"
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 52),
                FormatDistance(characterStats.DistanceFallen),
                $"({(characterStats.DistanceTravelled > 0.0 ? characterStats.DistanceFallen / characterStats.DistanceTravelled * 100.0 : 0.0):0.0}%)"
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 53),
                FormatDistance(characterStats.DistanceClimbed),
                $"({(characterStats.DistanceTravelled > 0.0 ? characterStats.DistanceClimbed / characterStats.DistanceTravelled * 100.0 : 0.0):0.0}%)"
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 54),
                FormatDistance(characterStats.DistanceFlown),
                $"({(characterStats.DistanceTravelled > 0.0 ? characterStats.DistanceFlown / characterStats.DistanceTravelled * 100.0 : 0.0):0.0}%)"
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 55),
                FormatDistance(characterStats.DistanceSwam),
                $"({(characterStats.DistanceTravelled > 0.0 ? characterStats.DistanceSwam / characterStats.DistanceTravelled * 100.0 : 0.0):0.0}%)"
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 56),
                FormatDistance(characterStats.DistanceRidden),
                $"({(characterStats.DistanceTravelled > 0.0 ? characterStats.DistanceRidden / characterStats.DistanceTravelled * 100.0 : 0.0):0.0}%)"
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 57),
                FormatDistance(characterStats.LowestAltitude)
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 58),
                FormatDistance(characterStats.HighestAltitude)
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 59),
                characterStats.DeepestDive.ToString("N1") + "m"
            );
            AddStat(stackPanelWidget, LanguageManager.Get(_typeName, 60), characterStats.Jumps.ToString("N0"));
            stackPanelWidget.Children.Add(
                new LabelWidget
                {
                    Text = LanguageManager.Get(_typeName, 61),
                    Font = font,
                    HorizontalAlignment = WidgetAlignment.Center,
                    Margin = new Vector2(0f, 10f),
                    Color = white
                }
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 62),
                (characterStats.TotalHealthLost * 100.0).ToString("N0") + "%"
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 63),
                characterStats.FoodItemsEaten.ToString("N0") + LanguageManager.Get(_typeName, 64)
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 65),
                characterStats.TimesWentToSleep.ToString("N0") + LanguageManager.Get(_typeName, 66)
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 67),
                (characterStats.TimeSlept / 1200.0).ToString("N1") + LanguageManager.Get(_typeName, 68)
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 69),
                characterStats.TimesWasSick.ToString("N0") + LanguageManager.Get(_typeName, 66)
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 70),
                characterStats.TimesPuked.ToString("N0") + LanguageManager.Get(_typeName, 66)
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 71),
                characterStats.TimesHadFlu.ToString("N0") + LanguageManager.Get(_typeName, 66)
            );
            stackPanelWidget.Children.Add(
                new LabelWidget
                {
                    Text = LanguageManager.Get(_typeName, 72),
                    Font = font,
                    HorizontalAlignment = WidgetAlignment.Center,
                    Margin = new Vector2(0f, 10f),
                    Color = white
                }
            );
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 73),
                characterStats.StruckByLightning.ToString("N0") + LanguageManager.Get(_typeName, 66)
            );
            var easiestModeUsed = characterStats.EasiestModeUsed;
            AddStat(
                stackPanelWidget,
                LanguageManager.Get(_typeName, 74),
                LanguageManager.Get("GameMode", easiestModeUsed.ToString())
            );
            if (characterStats.DeathRecords.Count > 0)
            {
                stackPanelWidget.Children.Add(
                    new LabelWidget
                    {
                        Text = LanguageManager.Get(_typeName, 75),
                        Font = font,
                        HorizontalAlignment = WidgetAlignment.Center,
                        Margin = new Vector2(0f, 10f),
                        Color = white
                    }
                );
                foreach (var deathRecord in characterStats.DeathRecords)
                {
                    AddStat(stackPanelWidget, $"Day {Math.Floor(deathRecord.Day) + 1.0:0}", "", deathRecord.Cause);
                }
            }
        }
        else
        {
            stackPanelWidget.Children.Add(
                new LabelWidget
                {
                    Text = LanguageManager.Get(_typeName, 81),
                    WordWrap = true,
                    Font = font2,
                    HorizontalAlignment = WidgetAlignment.Center,
                    TextAnchor = TextAnchor.HorizontalCenter,
                    Margin = new Vector2(20f, 10f),
                    Color = white
                }
            );
        }
    }

    public override void Update()
    {
        if (Children.Find<ButtonWidget>("More")!.IsClicked)
        {
            var list = new List<Tuple<string, Action>>();
            if (_adventureRestartExists &&
                GameManager.WorldInfo != null &&
                GameManager.WorldInfo.WorldSettings.GameMode == GameMode.Adventure)
            {
                list.Add(
                    new Tuple<string, Action>(
                        LanguageManager.Get(_typeName, 82),
                        delegate
                        {
                            DialogsManager.ShowDialog(
                                ParentWidget,
                                new MessageDialog(
                                    LanguageManager.Get(_typeName, 83),
                                    LanguageManager.Get(_typeName, 84),
                                    LanguageManager.Get("Usual", "yes"),
                                    LanguageManager.Get("Usual", "no"),
                                    delegate(MessageDialogButton result)
                                    {
                                        if (result == MessageDialogButton.Button1)
                                        {
                                            ScreensManager.SwitchScreen(
                                                "GameLoading",
                                                GameManager.WorldInfo,
                                                "AdventureRestart"
                                            );
                                        }
                                    }
                                )
                            );
                        }
                    )
                );
            }

            if (CommonLib.Net.IsServer)
            {
                list.Add(
                    new Tuple<string, Action>(
                        P2PRoomStatusDialog.Text("StatusTitle"),
                        () => DialogsManager.ShowDialog(ParentWidget, new P2PRoomStatusDialog(CommonLib.Net))
                    )
                );
            }

            list.Add(
                new Tuple<string, Action>(
                    LanguageManager.Get(_typeName, 87),
                    delegate
                    {
                        ScreensManager.SwitchScreen(
                            "Character",
                            CharacterScreen.Mode.Edit,
                            _componentCharacter.CharacterData
                        );
                    }
                )
            );
            list.Add(
                new Tuple<string, Action>(
                    LanguageManager.Get(_typeName, 88),
                    delegate { ScreensManager.SwitchScreen("Settings"); }
                )
            );
            list.Add(
                new Tuple<string, Action>(
                    LanguageManager.Get(_typeName, 89),
                    delegate { ScreensManager.SwitchScreen("Help"); }
                )
            );
            if ((Input.Devices & (WidgetInputDevice.Keyboard | WidgetInputDevice.Mouse)) != 0)
            {
                list.Add(
                    new Tuple<string, Action>(
                        LanguageManager.Get(_typeName, 90),
                        delegate { DialogsManager.ShowDialog(ParentWidget, new KeyboardHelpDialog()); }
                    )
                );
            }

            if ((Input.Devices & WidgetInputDevice.Gamepads) != 0)
            {
                list.Add(
                    new Tuple<string, Action>(
                        LanguageManager.Get(_typeName, 91),
                        delegate { DialogsManager.ShowDialog(ParentWidget, new GamepadHelpDialog()); }
                    )
                );
            }

            var dialog = new ListSelectionDialog(
                LanguageManager.Get(_typeName, 92),
                list,
                60f,
                t => ((Tuple<string, Action>)t).Item1,
                delegate(object t) { ((Tuple<string, Action>)t).Item2(); }
            );
            DialogsManager.ShowDialog(ParentWidget, dialog);
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("Resume")!.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }

        if (!Children.Find<ButtonWidget>("Quit")!.IsClicked)
        {
            return;
        }

        DialogsManager.HideDialog(this);
        if (CommonLib.WorkType == WorkType.Client)
        {
            ScreensManager.SwitchScreen("NetPlay");
        }
        else
        {
            GameManager.SaveProject(true, true);
            ScreensManager.SwitchScreen("Play");
        }

        CommonLib.Net.StopImmediate();
        GameManager.DisposeProject();
    }

    public static string FormatDistance(double value)
    {
        return value < 1000.0 ? $"{value:0}m" : $"{value / 1000.0:N2}km";
    }

    public void AddStat(ContainerWidget containerWidget, string title, string value1, string value2 = "")
    {
        AddStat(containerWidget, title, value1, value2, Color.White);
    }

    private void AddStat(ContainerWidget containerWidget, string title, string value1, string value2, Color color)
    {
        var font = ContentManager.Get<BitmapFont>("Fonts/Pericles");
        var gray = Color.Gray;
        containerWidget.Children.Add(
            new UniformSpacingPanelWidget
            {
                Direction = LayoutDirection.Horizontal,
                HorizontalAlignment = WidgetAlignment.Center,
                Children =
                {
                    new LabelWidget
                    {
                        Text = title + ":",
                        HorizontalAlignment = WidgetAlignment.Far,
                        Font = font,
                        Color = gray,
                        Margin = new Vector2(5f, 1f)
                    },
                    new StackPanelWidget
                    {
                        Direction = LayoutDirection.Horizontal,
                        HorizontalAlignment = WidgetAlignment.Near,
                        Children =
                        {
                            new LabelWidget
                            {
                                Text = value1,
                                Font = font,
                                Color = color,
                                Margin = new Vector2(5f, 1f)
                            },
                            new LabelWidget
                            {
                                Text = value2,
                                Font = font,
                                Color = gray,
                                Margin = new Vector2(5f, 1f)
                            }
                        }
                    }
                }
            }
        );
    }
}
