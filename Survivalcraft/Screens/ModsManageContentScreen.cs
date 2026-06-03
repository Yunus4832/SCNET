using System.Xml.Linq;

using Engine.Graphics;

namespace Game.Screens;

public class ModsManageContentScreen : Screen
{
    private enum StateFilter
    {
        UninstallState,
        InstallState
    }

    private const string _typeName = "ModsManageContentScreen";

    private readonly ButtonWidget _actionButton;

    private readonly ButtonWidget _actionButton2;

    private const string _androidDataPath = "android:/Android/data";

    private readonly bool _androidDataPathEnterEnabled;

    private CancellableBusyDialog? _cancellableBusyDialog;

    private bool _cancelScan;

    private readonly List<string> _commonPathList = [];

    private readonly string[] _commonPaths =
    [
        "android:/Download",
        "android:/Android/data/com.tencent.mobileqq/Tencent/QQfile_recv",
        "android:/Android/data/com.tencent.tim/Tencent/TIMfile_recv",
        "android:tencent/TIMfile_recv",
        "android:tencent/QQfile_recv",
        "android:/Quark/Download",
        "android:/BaiduNetdisk",
        "android:/UCDownloads",
        "android:/baidu/searchbox/downloads",
        Storage.CombinePaths(RunPath.ExternalPath, "NetMods")
    ];

    private int _count;

    private StateFilter _filter;

    private bool _firstEnterInstallScreen;

    private bool _firstEnterScreen;

    private readonly ButtonWidget _installFilterButton;

    private readonly List<ModInfo> _lastInstallModInfo = [];

    private string _lastPath = string.Empty;

    private readonly List<string> _latestScanModList = [];

    private readonly LabelWidget _modsContentLabel;

    private readonly ListPanelWidget _modsContentList;

    private string _path = string.Empty;

    private readonly List<string> _scanFailPaths = [];

    private readonly LabelWidget _topBarLabel;

    private readonly string _installPath = Storage.CombinePaths(RunPath.ExternalPath, "NetMods");

    private readonly List<ModInfo> _installModInfo = [];

    private readonly List<ModItem?> _installModList = [];

    private readonly ButtonWidget _uninstallFilterButton;

    private readonly List<ModItem?> _uninstallModList = [];

    private readonly string _uninstallPath = Storage.CombinePaths(RunPath.ExternalPath, "ModsCache");

    private bool _updatable;

    private readonly ButtonWidget _upDirectoryButton;

    public ModsManageContentScreen()
    {
        if (ModsManager.IsAndroid)
        {
            try
            {
                Storage.ListFileNames(_androidDataPath);
                _androidDataPathEnterEnabled = true;
            }
            catch
            {
                // Ignored
            }
        }

        _updatable = true;
        var node = ContentManager.Get<XElement>("Screens/ModsManageContentScreen");
        LoadContents(this, node);
        _modsContentList = Children.Find<ListPanelWidget>("ModsContentList")!;
        _topBarLabel = Children.Find<LabelWidget>("TopBar.Label")!;
        _modsContentLabel = Children.Find<LabelWidget>("ModsContentLabel")!;
        _actionButton = Children.Find<BevelledButtonWidget>("ActionButton")!;
        _actionButton2 = Children.Find<BevelledButtonWidget>("ActionButton2")!;
        Children.Find<BevelledButtonWidget>("ActionButton3")!.IsVisible = false;
        _uninstallFilterButton = Children.Find<BevelledButtonWidget>("UninstallFilter")!;
        _installFilterButton = Children.Find<BevelledButtonWidget>("InstallFilter")!;
        _upDirectoryButton = Children.Find<BevelledButtonWidget>("UpDirectory")!;
        _topBarLabel.Text = LanguageControl.Get(_typeName, 1);
        _uninstallFilterButton.Text = LanguageControl.Get(_typeName, 44);
        _installFilterButton.Text = LanguageControl.Get(_typeName, 45);
        _firstEnterScreen = false;
        _modsContentList.ItemWidgetFactory = delegate(object item)
        {
            var modItem = (ModItem)item;
            var node2 = ContentManager.Get<XElement>("Widgets/ExternalContentItem");
            var containerWidget = (ContainerWidget)LoadWidget(this, node2, null);
            var details = LanguageControl.Get(_typeName, 2);
            var color = Color.White;
            if (_latestScanModList.Contains(modItem.Name))
            {
                color = Color.Green;
            }

            if (modItem.ExternalContentEntry.Type == ExternalContentType.Mod)
            {
                if (modItem.ModInfo == null)
                {
                    details = LanguageControl.Get(_typeName, 68);
                    color = Color.Red;
                }
                else
                {
                    details = string.Format(LanguageControl.Get(_typeName, 3), modItem.ModInfo.Version,
                        modItem.ModInfo.Author, MathUtils.Round(modItem.ExternalContentEntry.Size / 1000));
                }
            }

            containerWidget.Children.Find<LabelWidget>("ExternalContentItem.Text")!.Text = modItem.Name;
            containerWidget.Children.Find<LabelWidget>("ExternalContentItem.Text")!.Color = color;
            containerWidget.Children.Find<LabelWidget>("ExternalContentItem.Details")!.Text = details;
            var iconWidget = containerWidget.Children.Find<RectangleWidget>("ExternalContentItem.Icon")!;
            iconWidget.Subtexture = modItem.Subtexture;
            iconWidget.Size = new Vector2(50, 50);
            iconWidget.Margin = new Vector2(10, 10);
            return containerWidget;
        };
        _modsContentList.ItemClicked += delegate(object item)
        {
            if (_modsContentList.SelectedItem != item)
            {
                return;
            }

            var modItem = (ModItem)item;
            if (modItem.ExternalContentEntry.Type == ExternalContentType.Directory &&
                modItem.ExternalContentEntry.Path != _installPath)
            {
                try
                {
                    if (modItem.ExternalContentEntry.Path != _androidDataPath)
                    {
                        SetPath(modItem.ExternalContentEntry.Path);
                        UpdateListWithBusyDialog();
                    }
                    else
                    {
                        DialogsManager.ShowDialog(
                            null,
                            new MessageDialog(
                                LanguageControl.Get(_typeName, 71),
                                LanguageControl.Get(_typeName, 72) + _androidDataPath,
                                LanguageControl.Ok
                            )
                        );
                    }
                }
                catch
                {
                    DialogsManager.ShowDialog(
                        null,
                        new MessageDialog(
                            LanguageControl.Get(_typeName, 4),
                            LanguageControl.Get(_typeName, 5) + "\n" + modItem.ExternalContentEntry.Path,
                            LanguageControl.Ok
                        )
                    );
                }
            }
            else if (modItem.ExternalContentEntry.Type == ExternalContentType.Mod)
            {
                if (_filter == StateFilter.UninstallState)
                {
                    string title;
                    string modDescription;
                    if (modItem.ModInfo != null)
                    {
                        title = modItem.ModInfo.Name;
                        modDescription = LanguageControl.Get(_typeName, 6) + modItem.ModInfo.Description + "\n" +
                                         LanguageControl.Get(_typeName, 7) + modItem.ModInfo.PackageName + "，" +
                                         LanguageControl.Get(_typeName, 8);
                    }
                    else
                    {
                        title = LanguageControl.Get(_typeName, 8);
                        modDescription = LanguageControl.Get(_typeName, 69);
                    }

                    DialogsManager.ShowDialog(
                        null,
                        new MessageDialog(
                            title,
                            modDescription,
                            LanguageControl.Get(_typeName, 9), LanguageControl.Get(_typeName, 10),
                            delegate(MessageDialogButton result)
                            {
                                if (result != MessageDialogButton.Button1)
                                {
                                    return;
                                }

                                Storage.DeleteFile(modItem.ExternalContentEntry.Path);
                                UpdateListWithBusyDialog();
                            }
                        )
                    );
                }
                else
                {
                    if (modItem.ModInfo == null)
                    {
                        return;
                    }

                    var modDescription = LanguageControl.Get(_typeName, 6) + modItem.ModInfo.Description + "\n" +
                                         LanguageControl.Get(_typeName, 7) + modItem.ModInfo.PackageName;
                    DialogsManager.ShowDialog(null, new MessageDialog(modItem.ModInfo.Name, modDescription,
                            LanguageControl.Get(_typeName, 60), LanguageControl.Get(_typeName, 10),
                            delegate(MessageDialogButton result)
                            {
                                if (result == MessageDialogButton.Button1)
                                {
                                    DialogsManager.ShowDialog(
                                        null,
                                        new MessageDialog(
                                            LanguageControl.Get(_typeName, 61),
                                            string.Empty,
                                            LanguageControl.Ok
                                        )
                                    );
                                }
                            }
                        )
                    );
                }
            }
        };
    }

    public override void Enter(object[] parameters)
    {
        if (!Storage.DirectoryExists(_uninstallPath))
        {
            Storage.CreateDirectory(_uninstallPath);
        }

        var busyDialog = new BusyDialog(LanguageControl.Get(_typeName, 26), LanguageControl.Get(_typeName, 32));
        DialogsManager.ShowDialog(null, busyDialog);
        foreach (var commonPath in _commonPaths)
        {
            if ((ModsManager.IsAndroid && commonPath.StartsWith("android:")) ||
                (!ModsManager.IsAndroid && !commonPath.StartsWith("android:")))
            {
                AddCommonPath(commonPath);
            }
        }

        var commonPathsFile = Storage.CombinePaths(_uninstallPath, "CommonPaths.txt");
        if (Storage.FileExists(commonPathsFile))
        {
            var stream = Storage.OpenFile(commonPathsFile, OpenFileMode.Read);
            var streamReader = new StreamReader(stream);
            while (streamReader.ReadLine() is { } line)
            {
                AddCommonPath(line.Replace("\n", "").Replace("\r", ""));
            }

            stream.Dispose();
        }

        if (!_firstEnterScreen)
        {
            _firstEnterScreen = true;
            var explanation = "";
            if (ModsManager.IsAndroid && !_androidDataPathEnterEnabled)
            {
                explanation += LanguageControl.Get(_typeName, 46) + "\n\n";
            }

            explanation += LanguageControl.Get(_typeName, 47);
            if (_commonPathList.Count > 0)
            {
                explanation += "\n\n" + LanguageControl.Get(_typeName, 48);
                for (var i = 0; i < _commonPathList.Count; i++)
                {
                    explanation += "\n" + (i + 1) + ". " + _commonPathList[i];
                }

                explanation += "\n\n" + LanguageControl.Get(_typeName, 12);
            }

            DialogsManager.ShowDialog(
                null,
                new MessageDialog(
                    LanguageControl.Get(_typeName, 14),
                    explanation,
                    LanguageControl.Get(_typeName, 15)
                )
            );
        }

        Task.Run(delegate
        {
            FastScanModFile(false);
            SetPath(_installPath);
            _filter = StateFilter.InstallState;
            UpdateList();
            SetPath(_uninstallPath);
            _filter = StateFilter.UninstallState;
            UpdateList();
            _updatable = true;
            _firstEnterInstallScreen = false;
            Dispatcher.Dispatch(delegate
            {
                foreach (var modInfo in _installModInfo)
                {
                    _lastInstallModInfo.Add(modInfo);
                }

                if (parameters.Length > 0 && (bool)parameters[0])
                {
                    SetPath(_installPath);
                    _filter = StateFilter.InstallState;
                    UpdateList();
                }

                UpdateList(true);
                DialogsManager.HideDialog(busyDialog);
            });
        });
    }

    public override void Leave()
    {
        _modsContentList.ClearItems();
        _installModInfo.Clear();
        _lastInstallModInfo.Clear();
        _uninstallModList.Clear();
        _installModList.Clear();
        _scanFailPaths.Clear();
        _latestScanModList.Clear();
        if (!Storage.DirectoryExists(_uninstallPath))
        {
            Storage.CreateDirectory(_uninstallPath);
        }

        var commonPathsFile = Storage.CombinePaths(_uninstallPath, "CommonPaths.txt");
        if (_commonPathList.Count > 0)
        {
            var stream = Storage.OpenFile(commonPathsFile, OpenFileMode.Create);
            var streamWriter = new StreamWriter(stream);
            foreach (var commonPath in _commonPathList)
            {
                streamWriter.WriteLine(commonPath);
            }

            streamWriter.Flush();
            stream.Dispose();
        }

        _commonPathList.Clear();
    }

    public override void Update()
    {
        _uninstallFilterButton.IsChecked = _filter != StateFilter.InstallState;
        _installFilterButton.IsChecked = _filter == StateFilter.InstallState;
        _uninstallFilterButton.Color = _filter == StateFilter.InstallState ? Color.White : Color.Green;
        _installFilterButton.Color = _filter == StateFilter.InstallState ? Color.Green : Color.White;
        _upDirectoryButton.IsVisible = _filter != StateFilter.InstallState;
        if (_filter != StateFilter.InstallState)
        {
            _actionButton2.Text = _path == _uninstallPath
                ? LanguageControl.Get(_typeName, 16)
                : LanguageControl.Get(_typeName, 17);
        }
        else
        {
            _actionButton2.Text = string.Empty;
        }

        ModItem? modItem = null;
        if (_modsContentList.SelectedIndex.HasValue)
        {
            modItem = _modsContentList.Items[_modsContentList.SelectedIndex.Value] as ModItem;
        }

        if (modItem is { ExternalContentEntry.Type: ExternalContentType.Mod })
        {
            _actionButton.Text = _filter == StateFilter.InstallState
                ? LanguageControl.Get(_typeName, 18)
                : LanguageControl.Get(_typeName, 19);
            _actionButton.IsEnabled = !(modItem.ModInfo == null && _filter != StateFilter.InstallState);
            _actionButton2.IsEnabled = _filter != StateFilter.InstallState;
        }
        else if (modItem is { ExternalContentEntry.Type: ExternalContentType.Directory })
        {
            _actionButton.IsEnabled = true;
            _actionButton.Text = LanguageControl.Get(_typeName, 20);
            _actionButton2.IsEnabled = _filter != StateFilter.InstallState;
        }
        else
        {
            _actionButton.Text = LanguageControl.Get(_typeName, 21);
            _actionButton.IsEnabled = false;
            _actionButton2.IsEnabled = _filter != StateFilter.InstallState;
        }

        if (_actionButton.IsClicked)
        {
            if (modItem != null && modItem.ExternalContentEntry.Type == ExternalContentType.Mod)
            {
                var fileName = Storage.GetFileName(modItem.ExternalContentEntry.Path);
                var installPathName = Storage.CombinePaths(_installPath, fileName);
                var uninstallPathName = modItem.ExternalContentEntry.Path;
                if (_filter == StateFilter.InstallState)
                {
                    string modDescription;
                    if (modItem.ModInfo != null)
                    {
                        modDescription = LanguageControl.Get(_typeName, 6) + modItem.ModInfo.Description + "\n" +
                                         LanguageControl.Get(_typeName, 7) + modItem.ModInfo.PackageName + "，" +
                                         LanguageControl.Get(_typeName, 8);
                    }
                    else
                    {
                        modDescription = LanguageControl.Get(_typeName, 70);
                    }

                    DialogsManager.ShowDialog(null, new MessageDialog(LanguageControl.Get(_typeName, 49), modDescription,
                        LanguageControl.Ok, LanguageControl.Cancel, delegate(MessageDialogButton result)
                        {
                            if (result == MessageDialogButton.Button1)
                            {
                                try
                                {
                                    Storage.DeleteFile(installPathName);
                                    UpdateListWithBusyDialog();
                                    _updatable = false;
                                }
                                catch (Exception e)
                                {
                                    DialogsManager.ShowDialog(null,
                                        new MessageDialog(LanguageControl.Get(_typeName, 50),
                                            LanguageControl.Get(_typeName, 51) + e.Message,
                                            LanguageControl.Get("Usual", "ok")
                                        )
                                    );
                                }
                            }
                        }));
                }
                else
                {
                    ModInfo? samePackmModInfo = null;
                    foreach (var modInfo in _installModInfo)
                    {
                        if (modInfo.PackageName == modItem.ModInfo?.PackageName)
                        {
                            samePackmModInfo = modInfo;
                        }
                    }

                    if (!Storage.FileExists(installPathName) && samePackmModInfo == null)
                    {
                        Storage.CopyFile(uninstallPathName, installPathName);
                        if (modItem.ModInfo != null)
                        {
                            _installModInfo.Add(modItem.ModInfo);
                        }

                        _installModList.Add(modItem);
                        DialogsManager.ShowDialog(
                            null,
                            new MessageDialog(
                                LanguageControl.Get(_typeName, 23),
                                fileName,
                                LanguageControl.Get("Usual", "ok")
                            )
                        );
                    }
                    else if (samePackmModInfo != null)
                    {
                        if (samePackmModInfo.Version == modItem.ModInfo?.Version)
                        {
                            DialogsManager.ShowDialog(
                                null,
                                new MessageDialog(
                                    LanguageControl.Get(_typeName, 52),
                                    LanguageControl.Get(_typeName, 53),
                                    LanguageControl.Get("Usual", "ok")
                                )
                            );
                        }
                        else
                        {
                            var tips = string.Format(LanguageControl.Get(_typeName, 54), modItem.ModInfo?.Version,
                                samePackmModInfo.Version);
                            DialogsManager.ShowDialog(
                                null,
                                new MessageDialog(LanguageControl.Get(_typeName, 55),
                                    tips,
                                    LanguageControl.Ok,
                                    LanguageControl.Cancel,
                                    delegate(MessageDialogButton result)
                                    {
                                        if (result != MessageDialogButton.Button1)
                                        {
                                            return;
                                        }

                                        foreach (var modItem3 in _installModList)
                                        {
                                            if (modItem3?.ModInfo?.PackageName == samePackmModInfo.PackageName)
                                            {
                                                try
                                                {
                                                    Storage.DeleteFile(modItem3.ExternalContentEntry.Path);
                                                    Storage.CopyFile(uninstallPathName, installPathName);
                                                    _updatable = true;
                                                }
                                                catch (Exception e)
                                                {
                                                    DialogsManager.ShowDialog(
                                                        null,
                                                        new MessageDialog(
                                                            LanguageControl.Get(_typeName, 56),
                                                            LanguageControl.Get(_typeName, 51) + e.Message,
                                                            LanguageControl.Get("Usual", "ok")
                                                        )
                                                    );
                                                }

                                                break;
                                            }
                                        }
                                    }));
                        }
                    }
                    else if (Storage.FileExists(installPathName))
                    {
                        DialogsManager.ShowDialog(
                            null,
                            new MessageDialog(
                                LanguageControl.Get(_typeName, 24),
                                fileName + LanguageControl.Get(_typeName, 57),
                                LanguageControl.Get("Usual", "ok")
                            )
                        );
                    }
                }
            }
            else if (modItem is { ExternalContentEntry.Type: ExternalContentType.Directory })
            {
                var busyDialog = new CancellableBusyDialog(LanguageControl.Get(_typeName, 26), true);
                ReadyForScan(busyDialog);
                Task.Run(delegate
                    {
                        var scanPath = modItem.ExternalContentEntry.Path;
                        var allCount = ScanModFile(scanPath, busyDialog);
                        DialogsManager.HideDialog(busyDialog);
                        if (allCount == 0)
                        {
                            DialogsManager.ShowDialog(
                                null,
                                new MessageDialog(
                                    LanguageControl.Get(_typeName, 4),
                                    LanguageControl.Get(_typeName, 33),
                                    LanguageControl.Get(_typeName, 10)
                                )
                            );
                        }
                        else
                        {
                            DialogsManager.ShowDialog(
                                null,
                                new MessageDialog(
                                    LanguageControl.Get(_typeName, 28),
                                    string.Format(LanguageControl.Get(_typeName, 29), allCount),
                                    LanguageControl.Get(_typeName, 30),
                                    LanguageControl.Get(_typeName, 31),
                                    delegate(MessageDialogButton result)
                                    {
                                        if (result != MessageDialogButton.Button1)
                                        {
                                            return;
                                        }

                                        SetPath(_uninstallPath);
                                        UpdateListWithBusyDialog();
                                    }
                                )
                            );
                        }
                    }
                );
            }
        }

        if (_actionButton2.IsClicked && _filter != StateFilter.InstallState)
        {
            if (_path == _uninstallPath)
            {
                if (_cancellableBusyDialog != null)
                {
                    DialogsManager.ShowDialog(null, _cancellableBusyDialog);
                    return;
                }

                _cancellableBusyDialog = new CancellableBusyDialog(LanguageControl.Get(_typeName, 26),
                    LanguageControl.Get(_typeName, 62), true);
                ReadyForScan(_cancellableBusyDialog);
                Task.Run(delegate
                {
                    string scanPath;
                    if (ModsManager.IsAndroid)
                    {
                        scanPath = "android:";
                    }
                    else
                    {
                        var systemPath = Storage.GetSystemPath(_path);
                        systemPath = systemPath.Replace("\\", "/");
                        var index = systemPath.IndexOf('/');
                        scanPath = string.Concat("system:", systemPath.AsSpan(0, index), "/");
                    }

                    var allCount = ScanModFile(scanPath, _cancellableBusyDialog);
                    DialogsManager.HideDialog(_cancellableBusyDialog);
                    _cancellableBusyDialog = null;
                    if (allCount == 0)
                    {
                        var tips = LanguageControl.Get(_typeName, 33);
                        if (_scanFailPaths.Count > 0)
                        {
                            tips += "\n\n" + LanguageControl.Get(_typeName, 58) + "\n";
                            tips = _scanFailPaths.Aggregate(tips, (current, p) => current + (p + "\n"));
                        }

                        DialogsManager.ShowDialog(null,
                            new MessageDialog(LanguageControl.Get(_typeName, 4), tips,
                                LanguageControl.Get(_typeName, 10)));
                    }
                    else
                    {
                        var tips = string.Format(LanguageControl.Get(_typeName, 35), allCount);
                        if (_scanFailPaths.Count > 0)
                        {
                            tips += "\n\n" + LanguageControl.Get(_typeName, 58) + "\n";
                            tips = _scanFailPaths.Aggregate(tips, (current, p) => current + (p + "\n"));
                        }

                        if (ScreensManager.CurrentScreen == this)
                        {
                            DialogsManager.ShowDialog(
                                null,
                                new MessageDialog(
                                    LanguageControl.Get(_typeName, 28),
                                    tips,
                                    LanguageControl.Get(_typeName, 30),
                                    string.Empty,
                                    delegate
                                    {
                                        SetPath(_uninstallPath);
                                        UpdateListWithBusyDialog();
                                    }
                                )
                            );
                        }
                        else
                        {
                            DialogsManager.ShowDialog(
                                null,
                                new MessageDialog(
                                    LanguageControl.Get(_typeName, 28),
                                    tips,
                                    LanguageControl.Ok
                                )
                            );
                        }
                    }
                });
            }
            else
            {
                SetPath(_uninstallPath);
                UpdateListWithBusyDialog();
            }
        }

        if (_uninstallFilterButton.IsClicked && _filter == StateFilter.InstallState)
        {
            _filter = StateFilter.UninstallState;
            SetPath(_uninstallPath);
            UpdateList(true);
        }

        if (_installFilterButton.IsClicked && _filter != StateFilter.InstallState)
        {
            _latestScanModList.Clear();
            _filter = StateFilter.InstallState;
            SetPath(_installPath);
            if (!_firstEnterInstallScreen)
            {
                _firstEnterInstallScreen = true;
                _updatable = true;
            }

            UpdateList(true);
        }

        if (_upDirectoryButton.IsClicked)
        {
            var directory = Storage.GetDirectoryName(_path);
            if (_path != "android:" && _path != "app:")
            {
                if (directory.StartsWith("system:") && !directory.Contains('/'))
                {
                    directory += "/";
                }

                SetPath(directory);
                UpdateListWithBusyDialog();
            }
            else if (_path == "app:")
            {
                var systemPath = Storage.GetSystemPath(_path);
                systemPath = systemPath.Replace("\\", "/");
                var index = systemPath.LastIndexOf('/');
                directory = "system:" + systemPath[..index];
                SetPath(directory);
                UpdateListWithBusyDialog();
            }
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            if (InstallModChange())
            {
                DialogsManager.ShowDialog(null, new MessageDialog(LanguageControl.Get(_typeName, 4),
                    LanguageControl.Get(_typeName, 38), LanguageControl.Get(_typeName, 39),
                    LanguageControl.Get(_typeName, 31),
                    delegate(MessageDialogButton result)
                    {
                        if (result == MessageDialogButton.Button1)
                        {
                            Environment.Exit(0);
                        }

                        if (result == MessageDialogButton.Button2)
                        {
                            ScreensManager.SwitchScreen("Content");
                        }
                    }));
            }
            else
            {
                ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
            }
        }
    }

    public void UpdateListWithBusyDialog(bool fast = false)
    {
        var busyDialog = new BusyDialog(LanguageControl.Get(_typeName, 43), string.Empty);
        DialogsManager.ShowDialog(null, busyDialog);
        Task.Run(delegate
        {
            UpdateList(fast);
            Dispatcher.Dispatch(delegate
            {
                UpdateList(true);
                DialogsManager.HideDialog(busyDialog);
            });
        });
    }

    public void UpdateList(bool fast = false)
    {
        _modsContentLabel.Text = LanguageControl.Get(_typeName, 40) + SetPathText(_path);
        if (!fast || _updatable)
        {
            SetModItemList();
            if (fast)
            {
                _updatable = false;
            }
        }

        _modsContentList.ClearItems();
        if (_filter == StateFilter.InstallState)
        {
            foreach (var modItem in _installModList.OfType<ModItem>())
            {
                _modsContentList.AddItem(modItem);
            }
        }
        else
        {
            foreach (var modItem in _uninstallModList.OfType<ModItem>())
            {
                _modsContentList.AddItem(modItem);
            }
        }
    }

    public void SetModItemList()
    {
        _updatable = true;
        if (_filter == StateFilter.InstallState)
        {
            _installModInfo.Clear();
            _installModList.Clear();
        }
        else
        {
            _uninstallModList.Clear();
        }

        try
        {
            var fileNameList = Storage.ListFileNames(_path);
            foreach (var fileName in fileNameList)
            {
                var extension = Storage.GetExtension(fileName);
                if (!string.IsNullOrEmpty(extension) && extension.ToLower() == ".scmod")
                {
                    var modItem = GetModItem(fileName, false);
                    if (modItem == null ||
                        (modItem.ModInfo != null && string.IsNullOrEmpty(modItem.ModInfo.PackageName)))
                    {
                        continue;
                    }

                    if (_filter == StateFilter.InstallState)
                    {
                        if (modItem.ModInfo != null)
                        {
                            _installModInfo.Add(modItem.ModInfo);
                        }

                        _installModList.Add(modItem);
                    }
                    else
                    {
                        _uninstallModList.Add(modItem);
                    }
                }
            }

            var directoryNameList = Storage.ListDirectoryNames(_path);
            foreach (var directoryName in directoryNameList)
            {
                var modItem = GetModItem(directoryName, true);
                if (_filter == StateFilter.InstallState)
                {
                    _installModList.Add(modItem);
                }
                else
                {
                    _uninstallModList.Add(modItem);
                }
            }
        }
        catch (Exception e)
        {
            Log.Warning("SetModItemList:" + e.Message);
        }
    }

    public void ReadyForScan(CancellableBusyDialog busyDialog)
    {
        _cancelScan = false;
        _scanFailPaths.Clear();
        _count = 0;
        DialogsManager.ShowDialog(null, busyDialog);
        busyDialog.ShowProgressMessage = false;
        busyDialog.Progress.Cancelled += delegate { _cancelScan = true; };
    }

    public int ScanModFile(string path, CancellableBusyDialog? busyDialog = null)
    {
        var validPath = path;
        if (_cancelScan)
        {
            return _count;
        }

        try
        {
            var systemPath = Storage.GetSystemPath(path);
            if (systemPath != Storage.GetSystemPath(_uninstallPath))
            {
                foreach (var fileName in Storage.ListFileNames(validPath))
                {
                    if (_cancelScan)
                    {
                        return _count;
                    }

                    if (validPath.EndsWith('/'))
                    {
                        validPath = path[..(validPath.Length - 1)];
                    }

                    if (busyDialog != null)
                    {
                        var showName = validPath;
                        if (validPath.Length > 40)
                        {
                            showName = validPath.Substring(0, 40) + "...";
                        }

                        busyDialog.SmallMessage = string.Format(LanguageControl.Get(_typeName, 59) + showName, _count);
                    }

                    var extension = Storage.GetExtension(fileName);
                    if (string.IsNullOrEmpty(extension) || extension.ToLower() != ".scmod")
                    {
                        continue;
                    }

                    var pathName = Storage.CombinePaths(validPath, fileName);
                    Stream? stream = null;
                    ModInfo? modInfo = null;
                    try
                    {
                        stream = Storage.OpenFile(pathName, OpenFileMode.Read);
                        var zipArchive = ZipArchive.ZipArchive.Open(stream);
                        foreach (var zipArchiveEntry in zipArchive.ReadCentralDir())
                        {
                            if (zipArchiveEntry.FilenameInZip == "modinfo.json")
                            {
                                var memoryStream = new MemoryStream();
                                zipArchive.ExtractFile(zipArchiveEntry, memoryStream);
                                memoryStream.Position = 0L;
                                modInfo = ModsManager.DeserializeJson<ModInfo>(
                                    ModsManager.StreamToString(memoryStream));
                                memoryStream.Dispose();
                                break;
                            }
                        }

                        stream.Dispose();
                    }
                    catch
                    {
                        // ignored
                    }

                    if (stream == null)
                    {
                        continue;
                    }

                    if (modInfo != null && string.IsNullOrEmpty(modInfo.PackageName))
                    {
                        continue;
                    }

                    var uninstallPathName = Storage.CombinePaths(_uninstallPath, fileName);
                    if (!Storage.FileExists(uninstallPathName))
                    {
                        Storage.CopyFile(pathName, uninstallPathName);
                        if (systemPath != Storage.GetSystemPath(_installPath))
                        {
                            Storage.DeleteFile(pathName);
                        }

                        AddCommonPath(validPath);
                        if (modInfo != null)
                        {
                            _latestScanModList.Add(fileName);
                            _count++;
                        }
                    }

                    stream.Close();
                }
            }

            foreach (var directory in Storage.ListDirectoryNames(path))
            {
                if (_cancelScan)
                {
                    return _count;
                }

                if (validPath.EndsWith("/"))
                {
                    validPath = path.Substring(0, validPath.Length - 1);
                }

                var subPath = Storage.CombinePaths(validPath, directory);
                ScanModFile(subPath, busyDialog);
            }
        }
        catch
        {
            _scanFailPaths.Add(validPath);
        }

        return _count;
    }

    public int FastScanModFile(bool showTips = true)
    {
        var allCount = 0;
        foreach (var commonPath in _commonPathList)
        {
            if ((ModsManager.IsAndroid && commonPath.StartsWith("android:")) ||
                (!ModsManager.IsAndroid && !commonPath.StartsWith("android:")))
            {
                try
                {
                    if (Storage.DirectoryExists(commonPath))
                    {
                        _count = 0;
                        var sucesssCount = ScanModFile(commonPath);
                        allCount += sucesssCount;
                    }
                }
                catch
                {
                    if (showTips)
                    {
                        DialogsManager.ShowDialog(null,
                            new MessageDialog(LanguageControl.Get(_typeName, 4),
                                string.Format(LanguageControl.Get(_typeName, 41), commonPath),
                                LanguageControl.Get(_typeName, 15)));
                    }
                }
            }
        }

        return allCount;
    }

    public ModItem? GetModItem(string fileName, bool isDirectory)
    {
        var pathName = Storage.CombinePaths(_path, fileName);
        var modItem = new ModItem
        {
            ExternalContentEntry = new ExternalContentEntry
            {
                Type = isDirectory ? ExternalContentType.Directory : ExternalContentType.Mod,
                Path = pathName,
                Size = isDirectory ? 0 : Storage.GetFileSize(pathName),
                Time = Storage.GetFileLastWriteTime(pathName)
            },
            Name = fileName,
            ModInfo = null,
            Subtexture = ExternalContentManager.GetEntryTypeIcon(isDirectory
                ? ExternalContentType.Directory
                : ExternalContentType.Mod)
        };
        if (isDirectory)
        {
            return modItem;
        }

        var stream = Storage.OpenFile(pathName, OpenFileMode.Read);
        try
        {
            var zipArchive = ZipArchive.ZipArchive.Open(stream);
            foreach (var zipArchiveEntry in zipArchive.ReadCentralDir())
            {
                if (zipArchiveEntry.FilenameInZip == "icon.png")
                {
                    var memoryStream = new MemoryStream();
                    zipArchive.ExtractFile(zipArchiveEntry, memoryStream);
                    memoryStream.Position = 0L;
                    modItem.Subtexture = new Subtexture(Texture2D.Load(memoryStream), Vector2.Zero, Vector2.One);
                    memoryStream.Dispose();
                }
                else if (zipArchiveEntry.FilenameInZip == "modinfo.json")
                {
                    var memoryStream = new MemoryStream();
                    zipArchive.ExtractFile(zipArchiveEntry, memoryStream);
                    memoryStream.Position = 0L;
                    modItem.ModInfo = ModsManager.DeserializeJson<ModInfo>(ModsManager.StreamToString(memoryStream))
                                      ?? throw new InvalidOperationException("ModInfo deserialization failed");
                    memoryStream.Dispose();
                }
            }
        }
        catch (Exception)
        {
            modItem = null;
        }
        finally
        {
            stream.Dispose();
        }

        return modItem;
    }

    public bool InstallModChange()
    {
        return _installModInfo.Count != _lastInstallModInfo.Count ||
               _installModInfo.Any(modInfo => !_lastInstallModInfo.Contains(modInfo));
    }

    public void SetPath(string path)
    {
        path = path.Replace("\\", "/");
        if (path == _path)
        {
            return;
        }

        _lastPath = _path;
        _path = path;
    }

    public string SetPathText(string path)
    {
        var newText = Storage.GetSystemPath(path);
        var arPath = path.Split(['/']);
        if (arPath.Length > 5)
        {
            newText = ".../" + arPath[^3] + "/" + arPath[^2] + "/" + arPath[^1];
        }

        return newText;
    }

    public void AddCommonPath(string path)
    {
        if (!_commonPathList.Contains(path) && !string.IsNullOrEmpty(path))
        {
            _commonPathList.Add(path);
        }
    }

    public void UpdateModFromCommunity(ModInfo modInfo)
    {
    }

    public class ModItem
    {
        public required ExternalContentEntry ExternalContentEntry;

        public ModInfo? ModInfo;

        public string Name = string.Empty;

        public required Subtexture Subtexture;
    }
}
