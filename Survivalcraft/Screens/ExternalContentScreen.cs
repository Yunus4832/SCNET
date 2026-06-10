using System.Xml.Linq;

using Game.ContentProviders;

namespace Game.Screens;

public class ExternalContentScreen : Screen
{
    private const string _typeName = "ExternalContentScreen";

    private readonly ButtonWidget _actionButton;

    private readonly ButtonWidget _changeProviderButton;

    private readonly ButtonWidget _copyLinkButton;

    private readonly LabelWidget _directoryLabel;

    private readonly ListPanelWidget _directoryList;

    private readonly Dictionary<string, bool> _downloadedFiles = new();

    private IExternalContentProvider _externalContentProvider = ExternalContentManager.DefaultProvider;

    private bool _listDirty;

    private readonly ButtonWidget _loginLogoutButton;

    private string _path = string.Empty;

    private readonly LabelWidget _providerNameLabel;

    private readonly ButtonWidget _upDirectoryButton;

    public ExternalContentScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/ExternalContentScreen");
        LoadContents(this, node);
        _directoryLabel = Children.Find<LabelWidget>("TopBar.Label")!;
        _directoryList = Children.Find<ListPanelWidget>("DirectoryList")!;
        _providerNameLabel = Children.Find<LabelWidget>("ProviderName")!;
        _changeProviderButton = Children.Find<ButtonWidget>("ChangeProvider")!;
        _loginLogoutButton = Children.Find<ButtonWidget>("LoginLogout")!;
        _upDirectoryButton = Children.Find<ButtonWidget>("UpDirectory")!;
        _actionButton = Children.Find<ButtonWidget>("Action")!;
        _copyLinkButton = Children.Find<ButtonWidget>("CopyLink")!;
        _directoryList.ItemWidgetFactory = delegate(object item)
        {
            var externalContentEntry2 = (ExternalContentEntry)item;
            var node2 = ContentManager.Get<XElement>("Widgets/ExternalContentItem");
            var containerWidget = (ContainerWidget)LoadWidget(this, node2, null);
            var fileName = Storage.GetFileName(externalContentEntry2.Path);
            var text = _downloadedFiles.ContainsKey(externalContentEntry2.Path)
                ? LanguageManager.Get(_typeName, 11)
                : string.Empty;
            var text2 = externalContentEntry2.Type != ExternalContentType.Directory
                ? $"{ExternalContentManager.GetEntryTypeDescription(externalContentEntry2.Type)} | {DataSizeFormatter.Format(externalContentEntry2.Size)} | {externalContentEntry2.Time:dd-MMM-yyyy HH:mm}{text}"
                : ExternalContentManager.GetEntryTypeDescription(externalContentEntry2.Type);
            containerWidget.Children.Find<RectangleWidget>("ExternalContentItem.Icon")!.Subtexture =
                ExternalContentManager.GetEntryTypeIcon(externalContentEntry2.Type);
            containerWidget.Children.Find<LabelWidget>("ExternalContentItem.Text")!.Text = fileName;
            containerWidget.Children.Find<LabelWidget>("ExternalContentItem.Details")!.Text = text2;
            return containerWidget;
        };
        _directoryList.ItemClicked += delegate(object item)
        {
            if (_directoryList.SelectedItem != item)
            {
                return;
            }

            if (item is not ExternalContentEntry { Type: ExternalContentType.Directory } externalContentEntry)
            {
                return;
            }

            SetPath(externalContentEntry.Path);
        };
    }

    public override void Enter(object[] parameters)
    {
        _directoryList.ClearItems();
        SetPath(string.Empty);
        _listDirty = true;
    }

    public override void Update()
    {
        if (_listDirty)
        {
            _listDirty = false;
            UpdateList();
        }

        ExternalContentEntry? externalContentEntry = null;
        if (_directoryList.SelectedIndex.HasValue)
        {
            externalContentEntry = _directoryList.Items[_directoryList.SelectedIndex.Value] as ExternalContentEntry;
        }

        if (externalContentEntry != null)
        {
            _actionButton.IsVisible = true;
            if (externalContentEntry.Type == ExternalContentType.Directory)
            {
                _actionButton.Text = LanguageManager.Get(_typeName, 1);
                _actionButton.IsEnabled = true;
                _copyLinkButton.IsEnabled = false;
            }
            else
            {
                _actionButton.Text = LanguageManager.Get(_typeName, 2);
                if (ExternalContentManager.IsEntryTypeDownloadSupported(
                        ExternalContentManager.ExtensionToType(
                            Storage.GetExtension(externalContentEntry.Path).ToLower())))
                {
                    _actionButton.IsEnabled = true;
                    _copyLinkButton.IsEnabled = true;
                }
                else
                {
                    _actionButton.IsEnabled = false;
                    _copyLinkButton.IsEnabled = false;
                }
            }
        }
        else
        {
            _actionButton.IsVisible = false;
            _copyLinkButton.IsVisible = false;
        }

        _directoryLabel.Text = _externalContentProvider.IsLoggedIn
            ? string.Format(LanguageManager.Get(_typeName, 3), _path)
            : LanguageManager.Get(_typeName, 4);
        _providerNameLabel.Text = _externalContentProvider.DisplayName;
        _upDirectoryButton.IsEnabled = _externalContentProvider.IsLoggedIn && _path != "/";
        _loginLogoutButton.Text = _externalContentProvider.IsLoggedIn
            ? LanguageManager.Get(_typeName, 5)
            : LanguageManager.Get(_typeName, 6);
        _loginLogoutButton.IsVisible = _externalContentProvider.RequiresLogin;
        _copyLinkButton.IsVisible = _externalContentProvider.SupportsLinks;
        _copyLinkButton.IsEnabled = externalContentEntry != null &&
                                    ExternalContentManager.IsEntryTypeDownloadSupported(externalContentEntry.Type);
        if (_changeProviderButton.IsClicked)
        {
            DialogsManager.ShowDialog(
                null,
                new SelectExternalContentProviderDialog(
                    LanguageManager.Get(_typeName, 7),
                    true,
                    delegate(IExternalContentProvider provider)
                    {
                        _externalContentProvider = provider;
                        _listDirty = true;
                        SetPath(string.Empty);
                    }
                )
            );
        }

        if (_upDirectoryButton.IsClicked)
        {
            var directoryName = Storage.GetDirectoryName(_path);
            SetPath(directoryName);
        }

        if (_actionButton.IsClicked && externalContentEntry != null)
        {
            if (externalContentEntry.Type == ExternalContentType.Directory)
            {
                SetPath(externalContentEntry.Path);
            }
            else
            {
                DownloadEntry(externalContentEntry);
            }
        }

        if (_copyLinkButton.IsClicked && externalContentEntry != null &&
            ExternalContentManager.IsEntryTypeDownloadSupported(externalContentEntry.Type))
        {
            var busyDialog = new CancellableBusyDialog(LanguageManager.Get(_typeName, 8), false);
            DialogsManager.ShowDialog(null, busyDialog);
            _externalContentProvider.Link(externalContentEntry.Path, busyDialog.Progress, delegate(string link)
            {
                DialogsManager.HideDialog(busyDialog);
                DialogsManager.ShowDialog(null, new ExternalContentLinkDialog(link));
            }, delegate(Exception error)
            {
                DialogsManager.HideDialog(busyDialog);
                DialogsManager.ShowDialog(
                    null,
                    new MessageDialog(
                        LanguageManager.Get("Usual", "error"),
                        error.Message,
                        LanguageManager.Get("Usual", "ok")
                    )
                );
            });
        }

        if (_loginLogoutButton.IsClicked)
        {
            if (_externalContentProvider.IsLoggedIn)
            {
                _externalContentProvider.Logout();
                SetPath(string.Empty);
                _listDirty = true;
            }
            else
            {
                ExternalContentManager.ShowLoginUiIfNeeded(_externalContentProvider, false, delegate
                {
                    SetPath(string.Empty);
                    _listDirty = true;
                });
            }
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Content");
        }
    }

    private void SetPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
#if ANDROID
            path = Storage.GetSystemPath(Storage.CombinePaths(RunPath.ExternalPath, "files"));
#endif
#if DESKTOP
            path = "files";
#endif
        }

        path = path.Replace("\\", "/");
        if (path == _path)
        {
            return;
        }

        _path = path;
        _listDirty = true;
    }

    private void UpdateList()
    {
        _directoryList.ClearItems();
        if (!_externalContentProvider.IsLoggedIn)
        {
            return;
        }

        var busyDialog = new CancellableBusyDialog(LanguageManager.Get(_typeName, 9), false);
        DialogsManager.ShowDialog(null, busyDialog);
        _externalContentProvider.List(_path, busyDialog.Progress, delegate(ExternalContentEntry entry)
        {
            DialogsManager.HideDialog(busyDialog);
            var list = new List<ExternalContentEntry>(entry.ChildEntries.Where(e => EntryFilter(e)).Take(1000));
            _directoryList.ClearItems();
            list.Sort(delegate(ExternalContentEntry e1, ExternalContentEntry e2)
            {
                if (e1.Type == ExternalContentType.Directory && e2.Type != ExternalContentType.Directory)
                {
                    return -1;
                }

                return e1.Type != ExternalContentType.Directory && e2.Type == ExternalContentType.Directory
                    ? 1
                    : string.CompareOrdinal(e1.Path, e2.Path);
            });
            foreach (var item in list)
            {
                _directoryList.AddItem(item);
            }
        }, delegate(Exception error)
        {
            DialogsManager.HideDialog(busyDialog);
            DialogsManager.ShowDialog(null,
                new MessageDialog(
                    LanguageManager.Get("Usual", "error"),
                    error.Message,
                    LanguageManager.Get("Usual", "ok")
                )
            );
        });
    }

    private void DownloadEntry(ExternalContentEntry entry)
    {
        var busyDialog = new CancellableBusyDialog(LanguageManager.Get(_typeName, 10), false);
        DialogsManager.ShowDialog(null, busyDialog);
        _externalContentProvider.Download(entry.Path, busyDialog.Progress, delegate(Stream stream)
            {
                busyDialog.LargeMessage = LanguageManager.Get(_typeName, 12);
                ExternalContentManager.ImportExternalContent(
                    stream,
                    entry.Type,
                    Storage.GetFileName(entry.Path),
                    delegate
                    {
                        stream.Dispose();
                        DialogsManager.HideDialog(busyDialog);
                    },
                    delegate(Exception error)
                    {
                        stream.Dispose();
                        DialogsManager.HideDialog(busyDialog);
                        DialogsManager.ShowDialog(null,
                            new MessageDialog(LanguageManager.Get("Usual", "error"), error.Message,
                                LanguageManager.Get("Usual", "ok")
                            )
                        );
                    }
                );
            },
            delegate(Exception error)
            {
                DialogsManager.HideDialog(busyDialog);
                DialogsManager.ShowDialog(
                    null,
                    new MessageDialog(
                        LanguageManager.Get("Usual", "error"),
                        error.Message,
                        LanguageManager.Get("Usual", "ok")
                    )
                );
            });
    }

    private static bool EntryFilter(ExternalContentEntry entry)
    {
        return entry.Type != ExternalContentType.Unknown;
    }
}
