using System.Xml.Linq;

using Game.Network;

namespace Game.Screens;

public class PlayScreen : Screen
{
    private const int _maxWorlds = 300;

    private const string _typeName = nameof(PlayScreen);

    private readonly ListPanelWidget _worldsListWidget;

    public PlayScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/PlayScreen");
        LoadContents(this, node);
        _worldsListWidget = Children.Find<ListPanelWidget>("WorldsList")!;
        var worldsListWidget = _worldsListWidget;
        worldsListWidget.ItemWidgetFactory = (Func<object, Widget>)Delegate.Combine(
            worldsListWidget.ItemWidgetFactory,
            (Func<object, Widget>)delegate (object item)
            {
                var worldInfo = (WorldInfo)item;
                var node2 = ContentManager.Get<XElement>("Widgets/SavedWorldItem");
                var containerWidget = (ContainerWidget)LoadWidget(this, node2, null);
                var labelWidget = containerWidget.Children.Find<LabelWidget>("WorldItem.Name")!;
                var labelWidget2 = containerWidget.Children.Find<LabelWidget>("WorldItem.Details")!;
                containerWidget.Tag = worldInfo;
                labelWidget.Text = worldInfo.WorldSettings.RunServer
                    ? worldInfo.WorldSettings.Name + LanguageManager.GetContentWidgets(_typeName, 11)
                    : worldInfo.WorldSettings.Name;
                labelWidget2.Text =
                    $"{DataSizeFormatter.Format(worldInfo.Size)} | {worldInfo.LastSaveTime.ToLocalTime():dd MMM yyyy HH:mm} | {(worldInfo.PlayerInfos.Count > 1
                        ? string.Format(LanguageManager.GetContentWidgets(_typeName, 9), worldInfo.PlayerInfos.Count)
                        : string.Format(LanguageManager.GetContentWidgets(_typeName, 10), 1))} | {LanguageManager.Get("GameMode", worldInfo.WorldSettings.GameMode.ToString())} | {LanguageManager.Get("EnvironmentBehaviorMode",
                        worldInfo.WorldSettings.EnvironmentBehaviorMode.ToString())}";
                if (worldInfo.ProjectFormatVersion != WorldVersions.ProjectFormatVersion)
                {
                    labelWidget2.Text = labelWidget2.Text + " | " +
                                        (string.IsNullOrEmpty(worldInfo.ProjectFormatVersion)
                                            ? LanguageManager.Get("Usual", "unknown")
                                            : "(" + worldInfo.ProjectFormatVersion + ")");
                }

                return containerWidget;
            });

        _worldsListWidget.ScrollPosition = 0f;
        _worldsListWidget.ScrollSpeed = 0f;
        _worldsListWidget.ItemClicked += delegate (object item)
        {
            if (_worldsListWidget.SelectedItem == item)
            {
                Play(item);
            }
        };
    }

    public override void Enter(object[] parameters)
    {
        var dialog = new BusyDialog(LanguageManager.GetContentWidgets(_typeName, 5), string.Empty);
        DialogsManager.ShowDialog(null, dialog);
        Task.Run(delegate
        {
            var selectedItem = (WorldInfo?)_worldsListWidget.SelectedItem;
            WorldsManager.UpdateWorldsList();
            var worldInfos = new List<WorldInfo>(WorldsManager.WorldInfos);
            worldInfos.Sort((w1, w2) => DateTime.Compare(w2.LastSaveTime, w1.LastSaveTime));
            Dispatcher.Dispatch(delegate
            {
                _worldsListWidget.ClearItems();
                foreach (var item in worldInfos)
                {
                    _worldsListWidget.AddItem(item);
                }

                if (selectedItem != null)
                {
                    _worldsListWidget.SelectedItem =
                        worldInfos.FirstOrDefault(wi => wi.DirectoryName == selectedItem.DirectoryName);
                }

                DialogsManager.HideDialog(dialog);
            });
        });
    }

    public override void Update()
    {
        if (_worldsListWidget.SelectedItem != null &&
            WorldsManager.WorldInfos.IndexOf((WorldInfo)_worldsListWidget.SelectedItem) < 0)
        {
            _worldsListWidget.SelectedItem = null;
        }

        Children.Find<LabelWidget>("TopBar.Label")!.Text = string.Format(
            LanguageManager.GetContentWidgets(_typeName, 6),
            _worldsListWidget.Items.Count);
        Children.Find("Play")!.IsEnabled = _worldsListWidget.SelectedItem != null;
        Children.Find("Properties")!.IsEnabled = _worldsListWidget.SelectedItem != null;
        if (Children.Find<ButtonWidget>("Play")!.IsClicked && _worldsListWidget.SelectedItem != null)
        {
            Play(_worldsListWidget.SelectedItem);
        }

        if (Children.Find<ButtonWidget>("NewWorld")!.IsClicked)
        {
            if (WorldsManager.WorldInfos.Count >= _maxWorlds)
            {
                DialogsManager.ShowDialog(
                    null,
                    new MessageDialog(
                        LanguageManager.GetContentWidgets(_typeName, 7),
                        string.Format(LanguageManager.GetContentWidgets(_typeName, 8), _maxWorlds),
                        LanguageManager.Get("Usual", "ok")
                    )
                );
            }
            else
            {
                ScreensManager.SwitchScreen("NewWorld");
                _worldsListWidget.SelectedItem = null;
            }
        }

        if (Children.Find<ButtonWidget>("Properties")!.IsClicked && _worldsListWidget.SelectedItem != null)
        {
            var worldInfo = (WorldInfo)_worldsListWidget.SelectedItem;
            ScreensManager.SwitchScreen("ModifyWorld", worldInfo.DirectoryName, worldInfo.WorldSettings);
        }

        if (Input is { Back: false, Cancel: false } && !Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            return;
        }

        ScreensManager.SwitchScreen("MainMenu");
        _worldsListWidget.SelectedItem = null;
    }

    private void Play(object item)
    {
        DialogsManager.HideAllDialogs();
        var worldInfo = (WorldInfo)item;
        PrepareWorldModsAndPlay(worldInfo);
        _worldsListWidget.SelectedItem = null;
    }

    private void PrepareWorldModsAndPlay(WorldInfo worldInfo)
    {
        var busyDialog = new BusyDialog(
            LanguageManager.GetContentWidgets(_typeName, 12),
            LanguageManager.GetContentWidgets(_typeName, 13));
        DialogsManager.ShowDialog(null, busyDialog);
        Task.Run(() =>
        {
            try
            {
                var result = ModRestartHelper.PrepareWorldSession(
                    worldInfo,
                    message => Dispatcher.Dispatch(() => busyDialog.SmallMessage = message));
                Dispatcher.Dispatch(() =>
                {
                    DialogsManager.HideDialog(busyDialog);
                    if (!result.RequiresRestart)
                    {
                        PlayPreparedWorld(worldInfo);
                        return;
                    }

                    ConfirmWorldModRestart(result);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Dispatch(() =>
                {
                    DialogsManager.HideDialog(busyDialog);
                    DialogsManager.Alert(
                        LanguageManager.GetContentWidgets(_typeName, 14),
                        string.Format(LanguageManager.GetContentWidgets(_typeName, 15), ex.Message));
                });
            }
        });
    }

    private static void ConfirmWorldModRestart(RemoteModSessionPreparation result)
    {
        DialogsManager.ShowDialog(
            null,
            new MessageDialog(
                LanguageManager.GetContentWidgets(_typeName, 16),
                string.Format(LanguageManager.GetContentWidgets(_typeName, 17), result.RestartReason),
                LanguageManager.GetContentWidgets(_typeName, 18),
                LanguageManager.Get("Usual", "cancel"),
                button =>
                {
                    if (button != MessageDialogButton.Button1)
                    {
                        return;
                    }

                    GameExitManager.RequestRestart(result.RemoteSession!, result.SessionProfile!);
                }));
    }

    private void PlayPreparedWorld(WorldInfo worldInfo)
    {
        if (worldInfo.WorldSettings.RunServer)
        {
            if (CommonLib.StartServer())
            {
                ScreensManager.SwitchScreen("GameLoading", worldInfo, string.Empty);
            }
            else
            {
                DialogsManager.ShowDialog(
                    this,
                    new MessageDialog(
                        LanguageManager.Get("Usual", "warning"),
                        LanguageManager.GetContentWidgets(_typeName, 19),
                        LanguageManager.Get("Usual", "ok"),
                        string.Empty,
                        _ =>
                        {
                            CommonLib.Net.StopImmediate();
                            DialogsManager.HideAllDialogs();
                        }
                    )
                );
            }
        }
        else
        {
            ScreensManager.SwitchScreen("GameLoading", worldInfo, string.Empty);
        }
    }
}
