using System.Xml.Linq;

using Game.Network;

namespace Game.Screens;

public class PlayScreen : Screen
{
    private const int _maxWorlds = 300;

    private const string _typeName = "PlayScreen";

    private readonly ListPanelWidget _worldsListWidget;

    public PlayScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/PlayScreen");
        LoadContents(this, node);
        _worldsListWidget = Children.Find<ListPanelWidget>("WorldsList")!;
        Children.Find<BevelledButtonWidget>("Play")!.Text = "创建服务器";
        var worldsListWidget = _worldsListWidget;
        worldsListWidget.ItemWidgetFactory = (Func<object, Widget>)Delegate.Combine(
            worldsListWidget.ItemWidgetFactory,
            (Func<object, Widget>)delegate(object item)
            {
                var worldInfo = (WorldInfo)item;
                var node2 = ContentManager.Get<XElement>("Widgets/SavedWorldItem");
                var containerWidget = (ContainerWidget)LoadWidget(this, node2, null);
                var labelWidget = containerWidget.Children.Find<LabelWidget>("WorldItem.Name")!;
                var labelWidget2 = containerWidget.Children.Find<LabelWidget>("WorldItem.Details")!;
                containerWidget.Tag = worldInfo;
                labelWidget.Text = worldInfo.WorldSettings.RunServer
                    ? worldInfo.WorldSettings.Name + "[联机档]"
                    : worldInfo.WorldSettings.Name;
                labelWidget2.Text =
                    $"{DataSizeFormatter.Format(worldInfo.Size)} | {worldInfo.LastSaveTime.ToLocalTime():dd MMM yyyy HH:mm} | {(worldInfo.PlayerInfos.Count > 1
                        ? string.Format(LanguageManager.GetContentWidgets(_typeName, 9), worldInfo.PlayerInfos.Count)
                        : string.Format(LanguageManager.GetContentWidgets(_typeName, 10), 1))} | {LanguageManager.Get("GameMode", worldInfo.WorldSettings.GameMode.ToString())} | {LanguageManager.Get("EnvironmentBehaviorMode",
                        worldInfo.WorldSettings.EnvironmentBehaviorMode.ToString())}";
                if (worldInfo.SerializationVersion != VersionsManager.SerializationVersion)
                {
                    labelWidget2.Text = labelWidget2.Text + " | " +
                                        (string.IsNullOrEmpty(worldInfo.SerializationVersion)
                                            ? LanguageManager.GetContentWidgets("Usual", "Unknown")
                                            : "(" + worldInfo.SerializationVersion + ")");
                }

                return containerWidget;
            });

        _worldsListWidget.ScrollPosition = 0f;
        _worldsListWidget.ScrollSpeed = 0f;
        _worldsListWidget.ItemClicked += delegate(object item)
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

        Children.Find<LabelWidget>("TopBar.Label")!.Text = string.Format(LanguageManager.GetContentWidgets(_typeName, 6),
            _worldsListWidget.Items.Count);
        Children.Find("Play")!.IsEnabled = _worldsListWidget.SelectedItem != null;
        Children.Find("Properties")!.IsEnabled = _worldsListWidget.SelectedItem != null;
        if (Children.Find<ButtonWidget>("Play")!.IsClicked && _worldsListWidget.SelectedItem != null)
        {
            var alertDialog = new AlertDialog(
                "提示",
                "是否创建服务器",
                "是",
                "否",
                () =>
                {
                    var info = (WorldInfo)_worldsListWidget.SelectedItem;
                    info.WorldSettings.RunServer = true;
                    Play(_worldsListWidget.SelectedItem);
                },
                DialogsManager.HideAllDialogs
            );
            DialogsManager.ShowDialog(null, alertDialog);
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
                        LanguageManager.GetContentWidgets("Usual", "ok")
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
        var busyDialog = new BusyDialog("准备世界模组", "正在检查所需模组...");
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
                        "模组准备失败",
                        $"无法准备该世界需要的模组。\n{ex.Message}");
                });
            }
        });
    }

    private static void ConfirmWorldModRestart(RemoteModSessionPreparation result)
    {
        DialogsManager.ShowDialog(
            null,
            new MessageDialog(
                "需要重启游戏",
                $"{result.RestartReason}\n\n是否现在重启？",
                "重启",
                "取消",
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
                        "提示",
                        "创建服务器失败：端口被占用",
                        "确定",
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
